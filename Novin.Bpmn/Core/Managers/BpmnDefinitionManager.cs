using Novin.Bpmn.Contracts;
using Novin.Bpmn.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Novin.Bpmn
{
    /// <summary>
    /// پیاده‌سازی مدیریت تعاریف فرآیندهای BPMN با پشتیبانی از نسخه‌بندی
    /// </summary>
    public class BpmnDefinitionManager : IBpmnDefinitionManager
    {
        private readonly IBpmnDefinitionAccessor _definitionAccessor;
        
        /// <summary>
        /// دسترسی به ذخیره‌ساز تعاریف فرآیند
        /// </summary>
        public IBpmnDefinitionAccessor DefinitionAccessor => _definitionAccessor;
        
        public BpmnDefinitionManager(IBpmnDefinitionAccessor definitionAccessor)
        {
            _definitionAccessor = definitionAccessor;
        }
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و محتوا
        /// </summary>
        public async Task<BpmnDefinitionInfo> DeployProcessAsync(string deploymentKey, string definitionXml, string label = null)
        {
            // بررسی ورودی‌ها
            if (string.IsNullOrWhiteSpace(deploymentKey))
            {
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            }
            
            if (string.IsNullOrWhiteSpace(definitionXml))
            {
                throw new ArgumentException("Definition XML cannot be empty", nameof(definitionXml));
            }
            
            // ابتدا بررسی می‌کنیم آیا این کلید قبلاً موجود است
            var existingDefinition = await _definitionAccessor.GetDefinitionAsync(deploymentKey);
            string newVersion = "1.0.0"; // نسخه پیش‌فرض
            
            if (existingDefinition != null)
            {
                // نسخه جدید را بر اساس نسخه قبلی محاسبه می‌کنیم
                newVersion = IncrementVersion(existingDefinition.Version);
            }
            
            // نسخه را در داخل XML تنظیم می‌کنیم (اختیاری - بسته به نیاز پروژه)
            var updatedXml = UpdateVersionInXml(definitionXml, newVersion);
            
            // ذخیره تعریف با نسخه جدید
            var definitionInfo = await _definitionAccessor.DeployDefinitionAsync(deploymentKey, updatedXml, label);
            
            // تنظیم نسخه در اطلاعات برگشتی
            definitionInfo.Version = newVersion;
            
            return definitionInfo;
        }
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و استریم محتوا
        /// </summary>
        public async Task<BpmnDefinitionInfo> DeployProcessAsync(string deploymentKey, Stream definitionStream, string label = null)
        {
            // خواندن محتوای استریم
            using var reader = new StreamReader(definitionStream);
            var definitionXml = await reader.ReadToEndAsync();
            
            // فراخوانی متد اصلی بارگذاری
            return await DeployProcessAsync(deploymentKey, definitionXml, label);
        }
        
        /// <summary>
        /// دریافت یک تعریف فرآیند با کلید
        /// </summary>
        public async Task<BpmnDefinitionInfo> GetProcessDefinitionAsync(string deploymentKey)
        {
            return await _definitionAccessor.GetDefinitionAsync(deploymentKey);
        }
        
        /// <summary>
        /// دریافت همه تعاریف فرآیندها
        /// </summary>
        public async Task<IEnumerable<BpmnDefinitionInfo>> GetAllProcessDefinitionsAsync()
        {
            return await _definitionAccessor.GetAllAsync();
        }
        
        /// <summary>
        /// حذف یک تعریف فرآیند
        /// </summary>
        public async Task<bool> DeleteProcessDefinitionAsync(string deploymentKey)
        {
            return await _definitionAccessor.DeleteAsync(deploymentKey);
        }
        
        /// <summary>
        /// اعتبارسنجی یک تعریف BPMN
        /// </summary>
        public async Task<bool> ValidateDefinitionAsync(string definitionXml)
        {
            try
            {
                // بررسی معتبر بودن XML
                var doc = XDocument.Parse(definitionXml);
                
                // بررسی وجود المان‌های ضروری BPMN
                var ns = doc.Root?.GetDefaultNamespace();
                if (ns == null)
                {
                    return false;
                }
                
                // بررسی المان‌های اصلی BPMN
                var processElements = doc.Descendants(ns + "process").ToList();
                if (!processElements.Any())
                {
                    return false;
                }
                
                // می‌توان بررسی‌های بیشتری اضافه کرد
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        #region Helper Methods
        
        /// <summary>
        /// افزایش نسخه بر اساس قوانین نسخه‌بندی معنایی
        /// </summary>
        private string IncrementVersion(string currentVersion)
        {
            // اگر نسخه فعلی خالی باشد، نسخه پیش‌فرض را برمی‌گردانیم
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                return "1.0.0";
            }
            
            // تجزیه نسخه به بخش‌های آن
            var parts = currentVersion.Split('.');
            
            // اگر بخش آخر عددی باشد، آن را افزایش می‌دهیم
            if (parts.Length > 0)
            {
                string lastPart = parts[parts.Length - 1];
                
                // بررسی می‌کنیم آیا بخش آخر عددی است
                if (int.TryParse(lastPart, out int lastNumber))
                {
                    // افزایش عدد
                    parts[parts.Length - 1] = (lastNumber + 1).ToString();
                }
                else
                {
                    // اگر عددی نباشد، عدد 1 را به انتهای آن اضافه می‌کنیم
                    Regex regex = new Regex(@"([a-zA-Z]+)(\d*)$");
                    var match = regex.Match(lastPart);
                    
                    if (match.Success)
                    {
                        string letters = match.Groups[1].Value;
                        string numbers = match.Groups[2].Value;
                        
                        if (string.IsNullOrEmpty(numbers))
                        {
                            // اگر هیچ عددی نباشد، عدد 1 را اضافه می‌کنیم
                            parts[parts.Length - 1] = letters + "1";
                        }
                        else
                        {
                            // اگر عدد وجود داشته باشد، آن را افزایش می‌دهیم
                            if (int.TryParse(numbers, out int num))
                            {
                                parts[parts.Length - 1] = letters + (num + 1).ToString();
                            }
                            else
                            {
                                // اگر تبدیل نشود، به انتها 1 اضافه می‌کنیم
                                parts[parts.Length - 1] = lastPart + "1";
                            }
                        }
                    }
                    else
                    {
                        // حالت پیش‌فرض، اضافه کردن عدد 1 به انتها
                        parts[parts.Length - 1] = lastPart + "1";
                    }
                }
            }
            else
            {
                // اگر بخش‌بندی نشده باشد، یک را برمی‌گردانیم
                return "1.0.0";
            }
            
            // بازسازی رشته نسخه
            return string.Join(".", parts);
        }
        
        /// <summary>
        /// به‌روزرسانی نسخه در XML تعریف فرآیند
        /// </summary>
        private string UpdateVersionInXml(string definitionXml, string version)
        {
            try
            {
                var doc = XDocument.Parse(definitionXml);
                var ns = doc.Root?.GetDefaultNamespace();
                
                if (ns != null)
                {
                    // یافتن المان definitions
                    var definitionsElement = doc.Root;
                    if (definitionsElement != null)
                    {
                        // تنظیم یا به‌روزرسانی ویژگی نسخه
                        var versionAttr = definitionsElement.Attribute("version");
                        if (versionAttr != null)
                        {
                            versionAttr.Value = version;
                        }
                        else
                        {
                            definitionsElement.Add(new XAttribute("version", version));
                        }
                    }
                }
                
                return doc.ToString();
            }
            catch
            {
                // در صورت خطا در تجزیه XML، همان XML اصلی را برمی‌گردانیم
                return definitionXml;
            }
        }
        
        #endregion
    }
} 