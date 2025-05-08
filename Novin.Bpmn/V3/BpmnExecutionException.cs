using System;

namespace Novin.Bpmn.V3
{
    /// <summary>
    /// کلاس استثنای اختصاصی برای خطاهای اجرای BPMN
    /// </summary>
    public class BpmnExecutionException : Exception
    {
        /// <summary>
        /// کد خطای BPMN
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// ایجاد نمونه جدید از استثنای اجرای BPMN
        /// </summary>
        /// <param name="message">پیام خطا</param>
        public BpmnExecutionException(string message) : base(message)
        {
            ErrorCode = "BPMN_ERROR";
        }

        /// <summary>
        /// ایجاد نمونه جدید از استثنای اجرای BPMN با کد خطا
        /// </summary>
        /// <param name="message">پیام خطا</param>
        /// <param name="errorCode">کد خطا</param>
        public BpmnExecutionException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode ?? "BPMN_ERROR";
        }

        /// <summary>
        /// ایجاد نمونه جدید از استثنای اجرای BPMN با استثنای داخلی
        /// </summary>
        /// <param name="message">پیام خطا</param>
        /// <param name="innerException">استثنای داخلی</param>
        public BpmnExecutionException(string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = "BPMN_ERROR";
        }

        /// <summary>
        /// ایجاد نمونه جدید از استثنای اجرای BPMN با کد خطا و استثنای داخلی
        /// </summary>
        /// <param name="message">پیام خطا</param>
        /// <param name="errorCode">کد خطا</param>
        /// <param name="innerException">استثنای داخلی</param>
        public BpmnExecutionException(string message, string errorCode, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode ?? "BPMN_ERROR";
        }
    }
} 