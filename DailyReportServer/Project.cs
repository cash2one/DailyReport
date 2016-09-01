using Higuchi.Net.Mail;
using Higuchi.Net.Smtp;
using Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Xml;

namespace DailyReportServer
{
	class Project
	{
		public static string DataProviderRoot = System.IO.Path.GetDirectoryName(Application.Current.GetType().Assembly.Location) + "\\";
		public static string HtmlProviderPath = System.IO.Path.GetDirectoryName(Application.Current.GetType().Assembly.Location) + "\\Report.html";

		public string DisplayName { get; set; }

		private Server server;
		private string source;

		// 数据
		private XmlDocument Document = new XmlDocument();

		// 数据源
		public XmlDataProvider DataProviderDate = new XmlDataProvider();
		public XmlDataProvider DataProviderList = new XmlDataProvider();

		// 日志回调
		public delegate void LogEvent(string format, params object[] args);
		public event LogEvent Log;

		// 日报发送时间
		private TimeSpan mailSendTime;

		// 空日志函数
		private void LogEmpty(string format, params object[] args)
		{
		}

		/// <summary>
		/// 初始化
		/// </summary>
		/// <param name="Source">文件路径</param>
		public void Init(String Name, String Source)
		{
			DisplayName = Name;

			source = Source;
			Log += LogEmpty;

			Document.Load(source);

			DataProviderDate.Document = Document;
			DataProviderDate.XPath = "/DailyReport/Storage/Days";

			DataProviderList.Document = Document;
			DataProviderList.XPath = "/DailyReport/Storage/Days/Report";

			// 添加所有日期
			IEnumerable<XmlNode> rows = Document.SelectNodes("/DailyReport/Storage/Days").Cast<XmlNode>().OrderBy(
				r => DateTime.Parse(r.Attributes["Date"].Value));

			if (rows.Count() == 0)
			{
				NewDailyReports();
			}
			else
			{
				DateTime lastDate = DateTime.MinValue;

				// 补所有没记录的日志
				foreach (XmlNode node in rows)
				{
					if (lastDate == DateTime.MinValue)
					{
						lastDate = DateTime.Parse(node.Attributes["Date"].Value);
					}
					else
					{
						DateTime date = DateTime.Parse(node.Attributes["Date"].Value);
						if (date - lastDate > TimeSpan.FromDays(1))
						{
							for (DateTime dt = lastDate + TimeSpan.FromDays(1); dt < date; dt += TimeSpan.FromDays(1))
								NewDailyReports(dt);
						}

						lastDate = date;
					}
				}

				// 补最后一次日志日期到当天的日志。
				for (DateTime dt = lastDate + TimeSpan.FromDays(1); dt <= DateTime.Today; dt += TimeSpan.FromDays(1))
					NewDailyReports(dt);
			}

			// 服务器监听
			XmlNode Port = Document.SelectSingleNode("/DailyReport/Config/Port");

			server = new Server(CreateSession);
			server.Start(IPAddress.Any, int.Parse(Port.InnerText), 10);

			XmlNode SendTime = Document.SelectSingleNode("/DailyReport/Config/Time");
			mailSendTime = TimeSpan.Parse(SendTime.InnerText);
		}

		/// <summary>
		/// 判断是否工作日
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		private bool IsWorkDays(DateTime date)
		{
			if (date.DayOfWeek == DayOfWeek.Sunday)
				return false;
			if (date.DayOfWeek == DayOfWeek.Saturday)
				return false;

			XmlNodeList Holidays = Document.SelectNodes("/DailyReport/Config/Holidays/Date");
			foreach (XmlNode holiday in Holidays)
			{
				DateTime from = DateTime.Parse(holiday.Attributes["From"].Value);
				DateTime to = DateTime.Parse(holiday.Attributes["To"].Value);

				if (from.Date <= date && date <= to.Date)
					return false;
			}

			return true;
		}

		public XmlNode NewDailyReports()
		{
			return NewDailyReports(DateTime.Today);
		}

		public XmlNode NewDailyReports(DateTime date)
		{
			// 筛选指定日期，指定角色的日报
			XmlNode nodeDays = Document.SelectSingleNode(string.Format("/DailyReport/Storage/Days[@Date='{0}']", date.ToString("yyyy年MM月dd日")));
			// 跳过休息日
			if (nodeDays == null && IsWorkDays(date))
			{
				// 生成当日的日报节点
				nodeDays = Document.CreateElement("Days");

				XmlAttribute attr = null;
				attr = Document.CreateAttribute("Date");
				attr.Value = date.ToString("yyyy年MM月dd日");
				nodeDays.Attributes.Append(attr);

				attr = Document.CreateAttribute("Commit");
				attr.Value = "False";
				nodeDays.Attributes.Append(attr);

				// 生成所有角色的日报节点
				XmlNodeList lstUser = Document.SelectNodes("/DailyReport/Config/Users/User");
				foreach (XmlNode User in lstUser)
				{
					XmlAttribute attrName = User.Attributes["Name"];
					if (attrName == null)
						continue;

					XmlAttribute attrGroup = User.Attributes["Group"];
					if (attrGroup == null)
						continue;

					XmlElement eleReport = Document.CreateElement("Report");

					attr = Document.CreateAttribute("Name");
					attr.Value = attrName.Value;
					eleReport.Attributes.Append(attr);

					nodeDays.AppendChild(eleReport);
				}

				XmlNode nodeRoot = Document.SelectSingleNode("/DailyReport/Storage");
				nodeRoot.AppendChild(nodeDays);

				Document.Save(source);
				DataProviderDate.Refresh();
				DataProviderList.Refresh();
			}

			return nodeDays;
		}

		internal void OnTimerCheck()
		{
			if (DateTime.Now > DateTime.Today + TimeSpan.Parse("08:00:00"))
			{
				NewDailyReports();
			}

			if (DateTime.Now > DateTime.Today + mailSendTime )
			{
				SendDailyReport(DateTime.Today - TimeSpan.FromDays(1));
			}
		}

		public void SendDailyReport(DateTime date, bool forceSend = false)
		{
			// 筛选指定日期，指定角色的日报
			XmlNode nodeDays = NewDailyReports(date);
			SendDailyReport(nodeDays, forceSend);
		}

		/// <summary>
		/// 发送日报
		/// </summary>
		/// <param name="nodeDays">日报节点</param>
		/// <param name="forceSend">是否强制发送</param>
		public void SendDailyReport(XmlNode nodeDays, bool forceSend = false)
		{
			if (nodeDays == null)
				return;

			if (forceSend == false && nodeDays.Attributes["Commit"].Value == "True")
				return;

			// 建立邮件列表
			List<string> mailto = new List<string>();

			// 建立分组列表
			Dictionary<string, List<string>> group = new Dictionary<string, List<string>>();

			// 检查用户是否全部提交了日报
			XmlNodeList lstUser = Document.SelectNodes("/DailyReport/Config/Users/User[@Alive='True']");
			foreach (XmlNode User in lstUser)
			{
				XmlAttribute attrName = User.Attributes["Name"];
				if (attrName == null)
					continue;

				XmlAttribute attrGroup = User.Attributes["Group"];
				if (attrGroup == null)
					continue;
				// 查找用户的报告
				XmlNode reportNode = nodeDays.SelectSingleNode(string.Format("Report[@Name='{0}']", attrName.Value));

				// 用户不存在则继续
				if (reportNode == null)
					continue;

				// 检查更新日期
				//if (forceSend == false && reportNode.Attributes["UpdateTime"] == null)
				//	return;

				List<string> lines;
				if (group.Keys.Contains(attrGroup.Value))
					lines = group[attrGroup.Value];
				else
					group.Add(attrGroup.Value, lines = new List<string>());

				// 添加用户日报
				lines.Add(string.Format("<tr Width='400'><th scope='row' id='r100'>{0}</th><td>{1}</td><td>{2}</td></tr>"
					, attrGroup.Value
					, attrName.Value
					, reportNode.InnerText.Replace("\r\n", "<br/>")));
			}

			// 发送日报邮件
			string subject = "[日报：" + DisplayName + "] " + nodeDays.Attributes["Date"].Value;
			string html = File.ReadAllText(HtmlProviderPath, Encoding.Default);

			Log("sending daily report ...");

			string body = "";
			foreach (var item in group)
				body += string.Join("\n", (List<string>)item.Value) + "\n";

			if( body.Length == 0 )
			{
				Log("没有有效日报内容。");
				return;
			}

			html = html.Replace("<!--report title-->", subject).Replace("<!--report body here-->", body);

			string mailFrom = "";
			// 发送地址
			XmlNode nodeFrom = Document.SelectSingleNode("/DailyReport/Config/Mail/Info/From");
			if (nodeFrom == null)
			{
				Log("寄件人地址未找到，邮件发送中断。");
				return;
			}

			mailFrom = nodeFrom.InnerText;

			foreach (XmlNode node in Document.SelectSingleNode("/DailyReport/Config/Mail/Info/To"))
				mailto.Add(node.InnerText);

			if (mailto.Count() == 0)
			{
				Log("收件人列表为空，邮件发送中断。");
				return;
			}

			// 发送邮件
			SendNetEmail(mailFrom, string.Join(";", mailto.ToArray()), subject, html, true);

			// 标记日报已发送
			nodeDays.Attributes["Commit"].Value = "True";
			Document.Save(source);
		}

		private void OnNewReport(Session c, Report report)
		{
			Log(string.Format("user {0} send dailyreport.", report.User));
			XmlNode nodeUser = Document.SelectSingleNode(string.Format("/DailyReport/Config/Users/User[@Name='{0}']", report.User));
			// 查看角色是否合法
			if (null == nodeUser)
			{
				Log(string.Format("user {0} is not validated.", report.User));
				SendErrorMsg(c, "用户不存在！");
				return;
			}

			// 查看提交日期是否合法
			DateTime date = DateTime.Parse(report.Date);
			if (date > DateTime.Now.Date)
				return;

			// 筛选指定日期，指定角色的日报
			XmlNode nodeDays = NewDailyReports(DateTime.Parse(report.Date));
			// 检查是否已提交
			XmlAttribute attrCommit = nodeDays.Attributes["Commit"];
			if (attrCommit == null)
				return;
			if (attrCommit.Value == null)
				return;

			// 查找用户的报告并更新
			XmlNode userReportNode = nodeDays.SelectSingleNode(string.Format("Report[@Name='{0}']", report.User));
			if (userReportNode != null)
			{
				// 设置日志更新时间
				XmlAttribute attrUpdateTime = Document.CreateAttribute("UpdateTime");
				attrUpdateTime.Value = DateTime.Now.ToString("yyyy年MM月dd日 hh:mm:ss");
				userReportNode.Attributes.Append(attrUpdateTime);
				userReportNode.InnerText = report.Content;
				Document.Save(source);

				DataProviderList.Refresh();
			}
		}

		/// <summary>
		/// 发送网络邮件，支持SSL
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <param name="subject"></param>
		/// <param name="body"></param>
		/// <param name="html"></param>
		private void SendNetEmail(string from, string to, string subject, string body, bool html = false)
		{
			XmlNode MailNode = Document.SelectSingleNode("/DailyReport/Config/Mail/Send");

			XmlNode node = MailNode.SelectSingleNode("Host");
			if (null == node)
			{
				Log("邮件发送服务器未设置，邮件发送中断。");
				return;
			}
			string host = node.InnerText;

			string port = "465";
			node = MailNode.SelectSingleNode("Port");
			if (null == node)
			{
				Log("邮件发送端口未设置，邮件发送中断。");
			}
			else
			{
				port = node.InnerText;
			}

			node = MailNode.SelectSingleNode("User");
			if (null == node)
			{
				Log("邮件发送账号未设置，邮件发送中断。");
				return;
			}

			string user = node.InnerText;

			node = MailNode.SelectSingleNode("Pass");
			if (null == node)
			{
				Log("邮件发送口令未设置，邮件发送中断。");
				return;
			}

			string pass = node.InnerText;

			SmtpClient cl = new SmtpClient();
			cl.Host = host;
			cl.Port = int.Parse(port);
			cl.Ssl = true;
			cl.UserName = user;
			cl.Password = pass;

			cl.AuthenticateMode = SmtpAuthenticateMode.Login;

			SmtpMessage mg = new SmtpMessage();
			mg.ContentType.Value = html ? "text/html" : "text/plain";
			mg.From = from;

			foreach (var receiver in to.Split(new char[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				mg.To.Add(new MailAddress(receiver));
			}

			mg.Subject = subject;
			mg.BodyText = body;

			try
			{
				cl.SendMail(mg);
				cl.Dispose();
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);
			}
		}

		private void CreateSession(Socket s)
		{
			Session c = new Session();
			c.OnRecvPacket += this.OnRecvPacket;
			c.OnDisconnect += this.OnDisconnect;
			c.OnSessionLog += this.Log.Target as Session.OnSessionLog_d;
			c.Accept(s, c);
		}

		private void OnRecvPacket(object UserData, BinaryReader br)
		{
			try
			{
				Session c = (Session)UserData;

				short packet_len = br.ReadInt16();
				short packet_msg = br.ReadInt16();

				Log("recv package len = {0}, msg = {1}", packet_len, packet_msg);
				switch (packet_msg)
				{
					case 1000: // 请求用户列表
						{
							MemoryStream ms = new MemoryStream();
							BinaryWriter bw = new BinaryWriter(ms);

							XmlNodeList lstUser = Document.SelectNodes("/DailyReport/Config/Users/User[@Name and @Alive='True']");

							bw.Write(lstUser.Count);
							foreach (XmlNode user in lstUser)
							{
								bw.Write(user.Attributes["Name"].Value);
							}

							c.Send(2000, ms.GetBuffer(), 0, (int)ms.Position);
						}
						break;
					case 1001: // 请求用户日报
						{
							string name = br.ReadString();

							XmlNodeList lstReport = Document.SelectNodes(string.Format("/DailyReport/Storage/Days/Report[@Name='{0}']", name));

							MemoryStream ms = new MemoryStream();
							BinaryWriter bw = new BinaryWriter(ms);

							bw.Write(name);
							bw.Write(lstReport.Count);
							foreach (XmlNode report in lstReport)
							{
								bw.Write(report.ParentNode.Attributes["Date"].Value);
								bw.Write(report.InnerText);
							}

							c.Send(2001, ms.GetBuffer(), 0, (int)ms.Position);
						}
						break;
					case 1002: // 查询缺失的日报
						{
							string name = br.ReadString();

							XmlNodeList lstReport = Document.SelectNodes(string.Format("/DailyReport/Storage/Days/Report[@Name='{0}']", name));

							MemoryStream ms = new MemoryStream();
							BinaryWriter bw = new BinaryWriter(ms);

							string content = "";
							foreach (XmlElement node in lstReport)
							{
								if (node.InnerText.Trim(new char[] { ' ', '\t' }).Length == 0)
									content += node.ParentNode.Attributes["Date"].Value + " 日报未提交\n";
							}
							bw.Write(content);

							c.Send(2002, ms.GetBuffer(), 0, (int)ms.Position);

						}
						break;
					case 3000: // 提交日报
						{
							Report report = new Report
							{
								User = br.ReadString(),
								Date = br.ReadString(),
								Content = br.ReadString()
							};

							OnNewReport(c, report);
						}
						break;
				}

				Log("proc msg done");
			}
			catch (Exception e)
			{
				Log(e.Message);
			}
		}

		private void OnDisconnect(object UserData)
		{
			Log("client disconnect.");
		}

		private void SendErrorMsg(Session c, string errorMessage)
		{

		}

		public void Close()
		{
			server.Close();
		}

		public void SaveDocument()
		{
			Document.Save(source);
		}
	}
}
