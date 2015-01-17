WinShareEnum
============

Windows Share Enumerator

download  https://github.com/nccgroup/WinShareEnum/raw/master/Info/WinShareEnum.exe

running against a range on my local (non-domain) network, with a few dummy shares. Please remember this is primarily designed for domain-ed / large networks:

![winshareenum running](http://i.imgur.com/1RswJvA.png?1)

options:
![winshareenum options](http://i.imgur.com/9y6V0WH.png?1)

  
A few things:
	
		
		
		Adding the username as ".\<username>" will authenticate locally instead of on the domain, ie. .\guest will attempt to auth to every server locally as the guest account instead of using <domain>\guest
		
		multiple IP ranges are supported (ie. 10.0-255.0.0-242 will enumerate the an unnecessarily large amount of IPs)

		Windows shares (particularly file searching) are slow. That's just how it is, have patience..

		The app needs .NET runtime 4.5 to run.
			Download 4.0 from  http://www.microsoft.com/en-gb/download/details.aspx?id=17718
			Download 4.5 from  http://www.microsoft.com/en-gb/download/details.aspx?id=30653

		clicking download a file will download it to the desktop, this WILL overwrite.
		shares coloured red are readable by everyone, blue are readable by the current user.
		When attempting to resolve SIDs on a domain, sometimes (I have no idea why) in certain situations, like being double VPN'd into a domain, the base domain won't resolve. Ie. use fully.qualified.domain\user where possible, and not domain\user.
		
		if there are any file filters that are missing (there are probably a lot) or good regular expressions used, please email me (click about --> version for my address) so i can update the standard list. likewise for bugs.
		
Example share copy output from home PC:

\\192.168.1.33\D:


	- Everyone
		--FullControl

	- NT AUTHORITY\Authenticated Users
		--Modify, Synchronize

	- NT AUTHORITY\Authenticated Users
		--AppendData, Synchronize

	- NT AUTHORITY\SYSTEM
		--FullControl

	- BUILTIN\Administrators
		--FullControl

	- BUILTIN\Users
		--ReadAndExecute, Synchronize


\\192.168.1.33\some folder only available to the user:


	- NT AUTHORITY\SYSTEM
		--FullControl

	- BUILTIN\Administrators
		--FullControl

	- OhNo\schaw_000
		--FullControl


cheers   
