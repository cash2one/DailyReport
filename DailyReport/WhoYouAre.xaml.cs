using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DailyReport
{
	/// <summary>
	/// Interaction logic for WhoYouAre.xaml
	/// </summary>
	public partial class WhoYouAre : Window
	{
		private string MyName = "Enter your name ...";
		public WhoYouAre()
		{
			InitializeComponent();
		}

		private void button_Click(object sender, RoutedEventArgs e)
		{
			MyName = UserName.Text.TrimStart(' ');
			if (MyName == "Enter your name ..." || MyName.Length == 0)
			{
				MessageBox.Show("Name cannot empty!");
				return;
			}

			this.DialogResult = true;
			Close();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			UserName.Text = MyName;
		}

		private void UserName_GotFocus(object sender, RoutedEventArgs e)
		{
			UserName.Text = "";
			UserName.Background = Brushes.White;
		}

		private void UserName_LostFocus(object sender, RoutedEventArgs e)
		{
			if (MyName.Length == 0)
			{
				MyName = "Enter your name ...";
				UserName.FontStyle = FontStyles.Italic;
				UserName.Background = Brushes.LightGray;
			}
		}
	}
}
