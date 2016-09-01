using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Text;

namespace DailyReportServer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			try
			{
				//处理非UI线程异常
				AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
			}
			catch (Exception ex)
			{
				string str = GetExceptionMsg(ex, string.Empty);
				MessageBox.Show(str, "系统错误");
			}
		}

		static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
		{
			string str = GetExceptionMsg(e.Exception, e.ToString());
			MessageBox.Show(str, "系统错误");
		}

		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			string str = GetExceptionMsg(e.ExceptionObject as Exception, e.ToString());
			MessageBox.Show(str, "系统错误");
		}

		/// <summary>
		/// 生成自定义异常消息
		/// </summary>
		/// <param name="ex">异常对象</param>
		/// <param name="backStr">备用异常消息：当ex为null时有效</param>
		/// <returns>异常字符串文本</returns>
		static string GetExceptionMsg(Exception ex, string backStr)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("****************************异常文本****************************");
			sb.AppendLine("【出现时间】：" + DateTime.Now.ToString());
			if (ex != null)
			{
				sb.AppendLine("【异常类型】：" + ex.GetType().Name);
				sb.AppendLine("【异常信息】：" + ex.Message);
				sb.AppendLine("【堆栈调用】：" + ex.StackTrace);
				sb.AppendLine("【其他错误】：" + ex.Source);
			}
			else
			{
				sb.AppendLine("【未处理异常】：" + backStr);
			}
			sb.AppendLine("***************************************************************");
			return sb.ToString();
		}
	}
}
