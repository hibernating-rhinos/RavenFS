#Enable Remote Differential Compression

RavenFS relies on built-in Windows *Remote Differential Compression* feature. You have to enable it to ensure proper working of RavenFS.

##Windows Server

1. Go to `Server Manager -> Features -> Add Features`.
2. Select `Remote Differential Compression` and install it.

![Figure 1: Enable RDC in Windows Server](images\enable-rdc-windows-server.png)

##Windows 7

1. Go to `Control Panel -> Programs and Features -> Turn Windows features on or off `
2. Check `Remote Differential Compression` and click OK. 

![Figure 2: Enable RDC in Windows 7](images\enable-rdc-windows-7.png)