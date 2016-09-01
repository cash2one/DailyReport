using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Text;

namespace DailyReport
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			// log.Fatal("An unexpected application exception occurred", e.Exception);

			string execute_file = this.GetType().Assembly.Location;

			string execute_path = Path.GetDirectoryName(execute_file);

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("---------------------------------");
			sb.AppendLine("Exception Datetime : " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
			sb.AppendLine("Exception Reason : " + e.Exception.Message);
			sb.AppendLine("---------------------------------");
			sb.AppendLine(e.Exception.StackTrace);
			sb.AppendLine("---------------------------------");
			sb.AppendLine("");

			File.AppendAllText( execute_path + "/exception.log", sb.ToString() );

			MessageBox.Show("An unexpected exception has occurred. Shutting down the application. Please check the log file for more details.");

			// Prevent default unhandled exception processing
			e.Handled = true;

			Environment.Exit(0);
		}
	}
}
