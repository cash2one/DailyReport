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

namespace DailyReportServer
{
	/// <summary>
	/// Interaction logic for Setting.xaml
	/// </summary>
	public partial class Setting : Window
	{
		private XmlDataProvider dataProvider = new XmlDataProvider();
		private XmlDocument docProject = new XmlDocument();

		private string docSource = null;
		public Setting()
		{
			InitializeComponent();
			dataProvider.Document = docProject;
			dataProvider.XPath = "/DailyReport/Config";

			ProjectSetting.DataContext = dataProvider;
		}

		private void Project_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			XmlNode selNode = Project.SelectedItem as XmlNode;
			if (selNode != null)
			{
				XmlAttribute attrSource = selNode.Attributes["Source"];
				if (attrSource == null)
					return;

				docSource = System.IO.Path.GetDirectoryName(Application.Current.GetType().Assembly.Location) + "\\" + attrSource.Value;

				try
				{
					docProject.Load(docSource);
					dataProvider.Refresh();
				}
				catch (XmlException err)
				{
					MessageBox.Show(err.Message);
				}
			}

		}

		private void OnNewUser(object sender, RoutedEventArgs e)
		{
			XmlNode users = docProject.SelectSingleNode("/DailyReport/Config/Users");
			if(users != null)
			{
				XmlNode newNode = docProject.CreateElement("User");
				XmlAttribute attrName = docProject.CreateAttribute("Name");
				XmlAttribute attrGroup = docProject.CreateAttribute("Group");
				XmlAttribute attrAlive = docProject.CreateAttribute("Alive");

				newNode.Attributes.Append(attrName);
				newNode.Attributes.Append(attrGroup);
				newNode.Attributes.Append(attrAlive);

				users.AppendChild(newNode);
			}
		}

		private void OnDelUser(object sender, RoutedEventArgs e)
		{
			XmlNode selNode = UserList.SelectedItem as XmlNode;
			if (selNode != null)
			{
				selNode.ParentNode.RemoveChild(selNode);

				dataProvider.Document.Save(docSource);
				dataProvider.Refresh();
			}
		}

		private void OnConfirme(object sender, RoutedEventArgs e)
		{
			dataProvider.Document.Save(docSource);
			Close();
		}
	}
}
