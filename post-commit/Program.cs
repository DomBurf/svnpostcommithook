using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Web;
using System.Xml;

//Author: Dominic Burford 
//Date: 20/05/2014

namespace post_commit
{
    /// <summary>
    /// This application is used to integrate SVN with BugTracker.NET.
    /// It does this by intercepting the SVN POST-COMMIT hook. SVN passes two arguments to a post commit hook:
    /// - the repository name / path
    /// - the revision number
    /// 
    /// The code contained within this class takes these arguments and extracts the commit details from SVN (in XML)
    /// and passes the XML revision information to a specific BugTracker.NET form (SVN_HOOK.ASPX) which adds the revision
    /// details to the BugTracker.NET database table BTNET.SVN_REVISIONS on DECRI-SQL-UAT
    /// For more information on SVN / BugTracker.NET integration see the following links:
    /// http://tortoisesvn.net/docs/release/TortoiseSVN_en/tsvn-dug-bugtracker.html
    /// http://ifdefined.com/doc_bug_tracker_subversion.html
    /// 
    /// The code can also handle being run as a scheduled task and / or ad hoc and determine the latest SVN revision itself if necessary. 
    /// Simply pass an empty string as the second argument (the revision) to enable this behaviour.
    /// 
    /// The post commit scripts are executed by the Visual SVN Server service which runs under the NETWORK SERVICE account.
    /// This application creates two files during execution:
    /// - an information log to store debugging / diagnostics information (post-commit-info.log)
    /// - an error log to store exceptions / errors (post-commit-error.log)
    /// Typically these will be located at 
    /// - C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Temp (if run as an SVN post commit hook)
    /// - User temp folder e.g. C:\Users\%username%\AppData\Local\Temp\ (if run as a scheduled task or ad hoc)
    /// </summary>
    class SvnPostCommit
    {
        //error log
        private const string ErrorLog = "post-commit-error.log";
        static TextWriter _twerr;

        //info log
        private const string Infolog = "post-commit-info.log";
        static TextWriter _twinfo;

        /// <summary>
        /// Main entry point for the application that integrates SVN commits with BugTracker.NET. 
        /// Adds the SVN commit information into BugTracker.NET via the associated bugid.
        /// </summary>
        /// <param name="args">Passed in from SVN via the post-commit hook
        /// arg[0] = full path to the repository
        /// </param>
        /// <returns>integer indicating success or failure. 0 is success.
        /// 0 = success
        /// 1 = no arguments passed to function
        /// 2 = unable to create the error and / or information logs
        /// 3 = incorrect numbr of arguments passed to function
        /// 4 = config file not found
        /// 5 = config values not set
        /// 6 = no XML returned from SVN for latest revision
        /// 7 = could not load XML 'revision' attribute for 'commit' XML node
        /// </returns>
        /// <remarks>
        /// Before prodceeding you will need to populate the config file with certain values:
        /// - pathtosvn: the full path to SVN.EXE
        /// - btnethookurl: the full URL to the BugTracker.NET hook page i.e. svn_hook.aspx
        /// - btusername: the BugTracker.NET username (must match the username specified in your BugTracket.NET web.config file)
        /// - btpassword: the BugTracker.NET password (must match the password specified in your BugTracket.NET web.config file)
        /// - svnrepourl: the URL to your SVN repo
        /// 
        /// Logging:
        /// All errors are written to a file called post-commit-error.log. This will be located in the user's Temp folder. 
        /// When running under SVN in a live production this may (depending on your installation) be running under the NETWORK SERVICE.
        /// In which case the file will be located here C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Temp.
        /// 
        /// Debugging / diagnostics information is written to a file called post-commit-info.log. See the comment above for its location.
        /// 
        /// The only time I have got this to fail is when processing a large SVN commit when there has been a large number of files
        /// and / or a verbose check-in comment leading to the querystring that is passed to BugTracker.NET to exceed its limits
        /// leading to an HTTP 404 error.
        /// </remarks>
        static int  Main(string[] args)
        {
            try
            {
                if (args == null) return 1;

                string tempFolder = Path.GetTempPath();

                _twerr = new StreamWriter(Path.Combine(tempFolder, ErrorLog), true);
                _twinfo = new StreamWriter(Path.Combine(tempFolder, Infolog), true);
                
                if (_twerr == null || _twinfo == null) return 2;

                WriteToInfoLog("");
                WriteToInfoLog("--------------------");
                WriteToInfoLog(string.Format("Starting execution of SVN Post Commit hook program at {0}", DateTime.Now.ToString(CultureInfo.InvariantCulture)));

                WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
                if (windowsIdentity != null)
                {
                    string userName = windowsIdentity.Name;
                    WriteToInfoLog(string.Format("Current user={0}", userName));
                }

                if (args.Length == 0)
                {
                    WriteToInfoLog("No arguments were passed to the application");
                    return 3;
                }

                //ensure we have the config values
                string configFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                WriteToInfoLog(string.Format("config file={0}", configFile));

                if (!File.Exists(configFile)) return 4;

                //values stored in config file
                ConfigValues configs = new ConfigValues();
                string pathToSvn = configs.PathToSvn;
                string btNetHookUrl = configs.BtNetHookUrl;
                string btUserName = configs.BtUserName;
                string btPassword = configs.BtPassword;
                string svnrepoUrl = configs.SvnrepoUrl;
                string lastSvnRevision = configs.LastSvnRevision;

                if (string.IsNullOrEmpty(pathToSvn) || string.IsNullOrEmpty(btNetHookUrl) || string.IsNullOrEmpty(btUserName)
                    || string.IsNullOrEmpty(btPassword) || string.IsNullOrEmpty(svnrepoUrl)) return 5;

                //If the revision was not passed in then we need to extract it from SVN
                string revision;
                if (string.IsNullOrEmpty(args[1]))
                {
                    //get the latest revision from SVN
                    Process proc = new Process
                    {
                        StartInfo =
                        {
                            FileName = Path.Combine(pathToSvn, "svn.exe"),
                            Arguments = string.Format("info {0} --xml", svnrepoUrl),
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        }
                    };
                    proc.Start();

                    //read the XML from standard output stream
                    string latestRevision = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    if (string.IsNullOrEmpty(latestRevision)) return 6;

                    //parse the XML for the latest revision number
                    //info/entry/commit/@revision
                    XmlDocument document = new XmlDocument();
                    document.LoadXml(latestRevision);
                    XmlAttribute xRevAttr = document.SelectSingleNode("//info/entry/commit/@revision") as XmlAttribute;
                    
                    if (xRevAttr == null) return 7;

                    revision = xRevAttr.Value;
                }
                else
                {
                    revision = args[1];
                }

                string repository = args[0];
                
                WriteToInfoLog(string.Format("Repository={0} revision={1}", repository, revision));
                WriteToInfoLog(string.Format("PathToSvn={0}", pathToSvn));
                WriteToInfoLog(string.Format("BtNetHookUrl={0}", btNetHookUrl));
                WriteToInfoLog(string.Format("BtUserName={0}", btUserName));
                WriteToInfoLog(string.Format("BtPassword={0}", btPassword));
                WriteToInfoLog(string.Format("SvnrepoUrl={0}", svnrepoUrl));
                WriteToInfoLog(string.Format("lastsvnrevision={0}", lastSvnRevision));
                
                //only proceed if the current revision and last revision are different
                if (lastSvnRevision != revision)
                {
                    //update the config file with the new revision number
                    UpdateConfigFile("lastsvnrevision", revision);

                    int lastRev = int.Parse(lastSvnRevision);
                    int currRev = int.Parse(revision);

                    //there may be several revisions since we last checked so iterate through all of them (only if run as a scheduled task / ad hoc)
                    for(int count = lastRev + 1; count <= currRev; ++count)
                    {
                        WriteToInfoLog(string.Format("Processing SVN revision={0}", count.ToString(CultureInfo.InvariantCulture)));

                        //execute SVN command for the specified revision 
                        //redirect standard output so as the resultant XML can be be read
                        Process p = new Process
                        {
                            StartInfo =
                            {
                                FileName = Path.Combine(pathToSvn, "svn.exe"),
                                Arguments = string.Format("log --verbose --xml -r {0} {1}", count, svnrepoUrl),
                                UseShellExecute = false,
                                RedirectStandardOutput = true
                            }
                        };
                        p.Start();

                        System.Threading.Thread.Sleep(2000);

                        //read the XML from standard output stream
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();

                        if (string.IsNullOrEmpty(output))
                        {
                            WriteToInfoLog("No XML was returned from SVN");
                            continue;
                        }
                        
                        if (!output.Contains("bugid"))
                        {
                            WriteToInfoLog("No BugTracker.NET information was found - nothing more to do");
                            continue;
                        }

                        output = output.Replace("\r\n", string.Empty);
                        WriteToInfoLog(string.Format("XML={0}", output));

                        //pass the XML to the BugTracket.NET hook URL
                        string urlParams = string.Format("?svn_log={0}&repo={1}&username={2}&password={3}", HttpUtility.UrlEncode(output), svnrepoUrl, btUserName, btPassword);

                        using (WebClient client = new WebClient())
                        {
                            string hookUrl = string.Concat(btNetHookUrl, urlParams);
                            WriteToInfoLog(string.Format("BugTracker.NET hook URL={0}", hookUrl));
                            string html = client.DownloadString(hookUrl);
                            WriteToInfoLog(html);
                            System.Threading.Thread.Sleep(5000);
                        }
                    }
                }
                else
                {
                    WriteToInfoLog("Last SVN revision and current SVN revision are the same - nothing more to do");
                }

                WriteToInfoLog(string.Format("Finished execution of SVN Post Commit hook program at {0}", DateTime.Now.ToString(CultureInfo.InvariantCulture)));
                
            }
            catch (Exception ex)
            {
                CatchException(ex);
            }
            finally
            {
                _twerr.Close();
                _twinfo.Close();
            }
            return 0;
        }

        static void CatchException(Exception ex)
        {
            if (ex == null) return;
            WriteToErrorLog(ex);
        }

        static void WriteToErrorLog(Exception ex)
        {
            _twerr.WriteLine("Error encountered at {0}", DateTime.Now.ToString(CultureInfo.InvariantCulture));
            _twerr.WriteLine(ex.Message);
            _twerr.WriteLine(ex.StackTrace);
        }

        private static void WriteToInfoLog(string format)
        {
            WriteToInfoLog(format, null);
        }

        static void WriteToInfoLog(string format, params object[] args)
        {
            if (args == null)
            {
                _twinfo.WriteLine(format);
            }
            else
            {
                _twinfo.WriteLine(format, args);
            }
        }

        static void UpdateConfigFile(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove(key);
            config.AppSettings.Settings.Add(key, value);
            config.Save(ConfigurationSaveMode.Minimal);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }

    class ConfigValues
    {
        public string PathToSvn = ConfigurationManager.AppSettings["pathtosvn"];
        public string BtNetHookUrl = ConfigurationManager.AppSettings["btnethookurl"];
        public string BtUserName = ConfigurationManager.AppSettings["btusername"];
        public string BtPassword = ConfigurationManager.AppSettings["btpassword"];
        public string SvnrepoUrl = ConfigurationManager.AppSettings["svnrepourl"];
        public string LastSvnRevision = ConfigurationManager.AppSettings["lastsvnrevision"];
    }
}
