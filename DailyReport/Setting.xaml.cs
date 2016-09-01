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
using System.Xml;
using System.IO;
using Microsoft.Win32;

namespace DailyReport
{
	/// <summary>
	/// Interaction logic for Setting.xaml
	/// </summary>
	public partial class Setting : Window
	{
		private XmlDataProvider dataProvider = null;
		private string ConfigFile = System.IO.Path.GetDirectoryName(Application.Current.GetType().Assembly.Location) + "\\Config.xml";
		public Setting()
		{
			InitializeComponent();
			dataProvider = FindResource("Config") as XmlDataProvider;
		}

		private void OnConfirm(object sender, RoutedEventArgs e)
		{
			dataProvider.Document.Save(ConfigFile);
			Close();
		}

		private void OnDelOld(object sender, RoutedEventArgs e)
		{
			XmlNode selNode = dataGrid.SelectedItem as XmlNode;
			if (selNode != null)
			{
				selNode.ParentNode.RemoveChild(selNode);

				dataProvider.Document.Save(ConfigFile);
				dataProvider.Refresh();
			}
		}

		private void OnAddNew(object sender, RoutedEventArgs e)
		{
			var doc = dataProvider.Document;
			XmlNode newNode = doc.CreateElement("Project");
			XmlAttribute attrName = doc.CreateAttribute("Name");
			XmlAttribute attrHost = doc.CreateAttribute("Host");
			XmlAttribute attrPort = doc.CreateAttribute("Port");

			newNode.Attributes.Append(attrName);
			newNode.Attributes.Append(attrHost);
			newNode.Attributes.Append(attrPort);

			XmlNode cfgNode = doc.SelectSingleNode("/Config");
			if( cfgNode != null )
				cfgNode.AppendChild(newNode);
		}

		private void OnAutoRunClicked(object sender, RoutedEventArgs e)
		{
			string fileName = Application.Current.GetType().Assembly.Location;
			RegistryKey reg = null;
			try
			{
				if (!System.IO.File.Exists(fileName))
					throw new Exception("该文件不存在!");
				String name = fileName.Substring(fileName.LastIndexOf(@"\") + 1);
				reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
				if (reg == null)
					reg = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
				if (AutoRunCheck.IsChecked == true)
					reg.SetValue(name, fileName);
				else
					reg.DeleteValue(name);
			}
			catch (Exception err)
			{
				MessageBox.Show(err.Message);
			}
			finally
			{
				if (reg != null)
					reg.Close();
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			try
			{
				string fileName = Application.Current.GetType().Assembly.Location;
				String regkname = fileName.Substring(fileName.LastIndexOf(@"\") + 1);

				RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
				if( reg == null )
				{
					AutoRunCheck.IsChecked = false;
					return;
				}

				string value = reg.GetValue(regkname) as string;
				AutoRunCheck.IsChecked = value != null ? value == fileName : false;
			}
			catch (Exception err)
			{
				MessageBox.Show(err.Message);
			}
		}
	}
}
