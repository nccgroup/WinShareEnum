WinShareEnum
============

Windows Share Enumerator

download  https://github.com/nccgroup/WinShareEnum/raw/master/Info/WinShareEnum.exe
  
A few things:
	
		
		
		Adding the username as ".\<username>" will authenticate locally instead of on the domain, ie. .\guest will attempt to auth to every server locally as the guest account instead of using <domain>\guest
		
		multiple IP ranges are supported (ie. 10.0-255.0.0-242 will enumerate the an unnecessarily large amount of IPs)

		Windows shares (particularly file searching) are slow. That's just how it is, have patience..

		The app needs .NET runtime 4.5 to run.
			Download 4.0 from  http://www.microsoft.com/en-gb/download/details.aspx?id=17718
			Download 4.5 from  http://www.microsoft.com/en-gb/download/details.aspx?id=30653

		clicking download a file will download it to the desktop, this WILL overwrite
		
		if there are any file filters that are missing (there are probably a lot) or good regular expressions used, please email me (click about --> version for my address) so i can update the standard list. likewise for bugs.
		

Thanks to the guys over at www.botnetchecks.com for their contributions..

cheers   
