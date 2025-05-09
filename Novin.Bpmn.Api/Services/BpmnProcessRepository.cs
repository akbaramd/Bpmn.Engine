using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Novin.Bpmn.V3;

namespace Novin.Bpmn.Api.Services
{
    /// <summary>
    /// پیاده‌سازی مخزن فرآیندها (می‌تواند با دیتابیس جایگزین شود)
    /// </summary>
    public class BpmnProcessRepository : IBpmnProcessRepository
    {
        // ذخیره موقت پردازش‌ها در حافظه - در محیط تولید با دیتابیس جایگزین شود
        private readonly ConcurrentDictionary<string, BpmnV3ProcessInstance> _processes = 
            new ConcurrentDictionary<string, BpmnV3ProcessInstance>();
        
        // نگاشت توکن‌ها به شناسه فرآیندها
        private readonly ConcurrentDictionary<Guid, string> _tokenToProcessMap = 
            new ConcurrentDictionary<Guid, string>();
            
        // مسیر ذخیره و بازیابی فایل‌ها
        private readonly string _storageDirectory;
        
        public BpmnProcessRepository(string storageDirectory = null)
        {
            _storageDirectory = storageDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProcessInstances");
            
            // اطمینان از وجود دایرکتوری ذخیره‌سازی
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه
        /// </summary>
        public async Task<BpmnV3ProcessInstance> GetProcessInstanceAsync(string processInstanceId)
        {
            // ابتدا بررسی در حافظه
            if (_processes.TryGetValue(processInstanceId, out var process))
            {
                return process;
            }
            
            // در صورت عدم وجود در حافظه، از فایل بخوان
            return await LoadProcessFromDiskAsync(processInstanceId);
        }
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه توکن
        /// </summary>
        public async Task<BpmnV3ProcessInstance> GetProcessInstanceByTokenAsync(Guid tokenId)
        {
            // یافتن شناسه فرآیند مرتبط با توکن
            if (_tokenToProcessMap.TryGetValue(tokenId, out var processId))
            {
                return await GetProcessInstanceAsync(processId);
            }
            
            // جستجو در تمام فرآیندهای موجود در حافظه
            foreach (var process in _processes.Values)
            {
                if (process.Tokens.Any(t => t.Id == tokenId))
                {
                    _tokenToProcessMap[tokenId] = process.Id;
                    return process;
                }
            }
            
            // جستجو در فایل‌های ذخیره شده
            var files = Directory.GetFiles(_storageDirectory, "*.json");
            foreach (var file in files)
            {
                // بررسی آیا فرآیند قبلاً بارگذاری شده است
                var processInstanceId = Path.GetFileNameWithoutExtension(file);
                if (_processes.ContainsKey(processInstanceId))
                {
                    continue;
                }
                
                var process = await LoadProcessFromDiskAsync(processInstanceId);
                if (process != null && process.Tokens.Any(t => t.Id == tokenId))
                {
                    _tokenToProcessMap[tokenId] = process.Id;
                    return process;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// ذخیره یک نمونه فرآیند
        /// </summary>
        public async Task SaveProcessInstanceAsync(BpmnV3ProcessInstance processInstance)
        {
            if (processInstance == null)
            {
                throw new ArgumentNullException(nameof(processInstance));
            }
            
            // ذخیره در حافظه
            _processes[processInstance.Id] = processInstance;
            
            // به‌روزرسانی نگاشت توکن‌ها
            foreach (var token in processInstance.Tokens)
            {
                _tokenToProcessMap[token.Id] = processInstance.Id;
            }
            
            // ذخیره در فایل
            await SaveProcessToDiskAsync(processInstance);
        }
        
        /// <summary>
        /// بارگذاری یک فرآیند از دیسک
        /// </summary>
        private async Task<BpmnV3ProcessInstance> LoadProcessFromDiskAsync(string processInstanceId)
        {
            var filePath = GetFilePathForProcess(processInstanceId);
            if (!File.Exists(filePath))
            {
                return null;
            }
            
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                // این متد نیاز به پیاده‌سازی سریالایزر/دیسریالایزر سفارشی دارد
                // در پیاده‌سازی واقعی باید سریالایزرهای مناسب ایجاد شوند
                var process = JsonSerializer.Deserialize<BpmnV3ProcessInstance>(json, options);
                
                // ذخیره در حافظه برای دسترسی سریع‌تر
                if (process != null)
                {
                    _processes[processInstanceId] = process;
                }
                
                return process;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading process {processInstanceId}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// ذخیره یک فرآیند در دیسک
        /// </summary>
        private async Task SaveProcessToDiskAsync(BpmnV3ProcessInstance processInstance)
        {
            var filePath = GetFilePathForProcess(processInstance.Id);
            
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                // این متد نیاز به پیاده‌سازی سریالایزر سفارشی دارد
                var json = JsonSerializer.Serialize(processInstance, options);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving process {processInstance.Id}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// دریافت مسیر فایل برای یک فرآیند
        /// </summary>
        private string GetFilePathForProcess(string processInstanceId)
        {
            return Path.Combine(_storageDirectory, $"{processInstanceId}.json");
        }
    }
}