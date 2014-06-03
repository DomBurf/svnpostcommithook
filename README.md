svnpostcommithook
=================

SVN to BugTracker.NET post hook integration

This application is used to integrate the version control system SVN (or Subversion) with the defect tracking system BugTracker.NET (developed by Corey Trager). 
It does this by intercepting the SVN POST-COMMIT hook. SVN passes two arguments to a post commit hook:
 - the repository name / path
 - the revision number
     

It takes the SVN commit information as entered by the developer when they perform an SVN commit and adds it to the 
corresponding bug in BugTracker.NET. 

This therefore allows all changes made for a particular bug to be traced in BugTracker.NET i.e. all code check-ins can be traced back to the originating bug.

It can be run either as an SVN post-commit hook or as a scheduled task (which runs in the background polling for SVN updates).

BugTracker.NET comes supplied with a Python equivalent of this application, but I couldn't get it to work, so I wrote an alternative in my preferred programming language (C#). I then extended the original Python functionality by adding the ability to be run as a scheduled task. It creates an error log and an information log (to tell you what it has done - useful for diagnostics if you need help getting it working).

Usage
=================

The application can be run as either an SVN post commit hook event or as a scheduled task. Here's an explanation of each. 

1. To use the app as an SVN hook: Copy the file post-commit.exe to your SVN repository's hook folder. SVN will then run the application and pass it two arguments.
 - the repository name / path
 - the revision number

2. To use the app as a scheduled task: Copy the file to any desired location on your SVN server and create a scheduled task that will will execute it. You will need to pass the following arguments to it.
 - the repository name / path
 - empty string (by leaving the revision number empty the application will determine the last revision number by querying SVN)

In both cases you will need to populate the config file with the following values.
 - pathToSvn: the path to SVN.EXE
 - BtNetHookUrl: the full URL to the BugTracker.NET hook form SVN_HOOK.ASPX
 - BtUsername: the BugTracker.NET user which will perform the operation ((needs to match the user specifgied in your BugTracker.NET web.config)
 - BtPassword: the BugTracker.NET password of the user which will perform the operation
 - SvnrepoUrl: the URL to your SVN repository
 - lastSvnRevision: the SVN revision last processed by the app (maintained by the app can can be set to your most recent revision for new installations. can be set to 0 but this will be pointless as your previous SVN check-ins will not contain BugTracker.NET information)

See my own vales for example usage.

Notes
=================
The information and error logs are called post-commit-info.log and post-commit-error.log respectively. They are created in the user's TEMP folder e.g. C:\Users\%username%\AppData\Local\Temp.

When running under SVN as a post commit hook, this may (depending on your installation) be running under the NETWORK SERVICE. In which case the file will be located here C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Temp.

You may want to periodically clear down the information log that is created by the application. It may become large over time, especially with larger teams. I have a Windows batch file that clears it down daily.
