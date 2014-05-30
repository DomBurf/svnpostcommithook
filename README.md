svnpostcommithook
=================

SVN to BugTracker.NET post hook integration

This application is used to integrate the version control system SVN (or Subversion) with the defect tracking system BugTracker.NET. 
It does this by intercepting the SVN POST-COMMIT hook. SVN passes two arguments to a post commit hook:
 - the repository name / path
 - the revision number
     

It takes the SVN commit information as entered by the developer when they perform an SVN commit and adds it to the 
corresponding bug in BugTracker.NET. 

This therefore allows all changes made for a particular bug to be traced in BugTracker.NET.

It can be run either as an SVN post-commit hook or as a scheduled task (which runs in the background polling for SVN updates).

BugTracker.NET comes supplied with a Python equivalent of this application, but I couldn't get it to work, so I wrote an alrernative in my preferred programming language (C#). I then extended the original Python functionality by adding the ability to be run as a scheduled task. It creates an error log and an information log (to tell you what it has done - useful for diagnostics if you need help getting it working).

Usage
=================

The application can be run as either an SVN post commit hook event or as a scheduled task. Here's an explanation of each. 

1. Copy the file post-commit.exe to your SVN repository's hook folder. SVN will then run the application and pass it two arguments.
 - the repository name / path
 - the revision number

2. Copy the file to any desired location on your SVN server and create a scheduled task that will will execute it. You will need to pass the following arguments to it.
 - the repository name / path
 - empty string (by leaving the revision number empty the application will determine the last revision number by querying SVN)



