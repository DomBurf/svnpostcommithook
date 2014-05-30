     Author - Dominic Burford
	 Date - 29/05/2014

	 NOTES
	 
	 This application is used to integrate SVN with BugTracker.NET. 
	 It does this by intercepting the SVN POST-COMMIT hook. SVN passes two arguments to a post commit hook:
     - the repository name / path
     - the revision number
     
     The code contained within this class takes these arguments and extracts the commit details from SVN (in XML)
     and passes the XML revision information to a specific BugTracker.NET form (SVN_HOOK.ASPX) which adds the revision
     details to the BugTracker.NET database table SVN_REVISIONS.
     
	 For more information on SVN / BugTracker.NET integration see the following links:

     http://tortoisesvn.net/docs/release/TortoiseSVN_en/tsvn-dug-bugtracker.html
     http://ifdefined.com/doc_bug_tracker_subversion.html
     
     The code can also handle being run as a scheduled task and / or ad hoc and determine the latest SVN revision itself if necessary. 
     Simply pass an empty string as the second argument (the revision) to enable this behaviour.
     
     The post commit scripts are executed by the Visual SVN Server service which runs under the NETWORK SERVICE account.
     This application creates two files during execution:
     - an information log to store debugging / diagnostics information (post-commit-info.log)
     - an error log to store exceptions / errors (post-commit-error.log)
     
	 Typically these will be located at 
     - C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Temp (if run as an SVN post commit hook)
     - User temp folder e.g. C:\Users\%username%\AppData\Local\Temp\ (if run as a scheduled task or ad hoc)

