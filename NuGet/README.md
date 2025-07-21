# How to Install Prebuilt NuGet Packages (PostgreSQL package shown in this example): 

Follow these instructions to install new / updated NuGet Packages into your project.

# Step 1
Copy and paste the NuGet Package into the location shown below.

C:\TwinCAT\Functions\TE2000-HMI-Engineering\References

<img width="919" height="496" alt="image" src="https://github.com/user-attachments/assets/ba87c97a-85da-409b-93cb-c2412a758367" />

# Step 2 
Once placed in the folder, open your project / solution in XAE / Visual Studio.  

Navigate to References, right click and select "Manage NuGet Packages".

<img width="611" height="437" alt="image" src="https://github.com/user-attachments/assets/b0f51072-affd-4c33-bd96-4c7b4d7e5cea" />

# Step 3
Select Package Source location

This sets where NuGet package manager will look for NuGet Packages.

In this case we will be pointing to our local source, which is listed as "TwinCAT HMI Customer".

<img width="1371" height="308" alt="image" src="https://github.com/user-attachments/assets/764c5a8e-6ca4-47c8-a2af-936cb52a7d91" />

# Step 4
Install or Update the desired Nuget Package.

In this example, the currently Installed Nuget package is at 1.12.760.5905, we will install the newly added version 1.12.760.5909.

Select the new version by clicking the "Version" drop down and navigating to the new package.

<img width="916" height="624" alt="image" src="https://github.com/user-attachments/assets/2c71ad95-ad3b-4144-b431-9ed3cb92a428" />

# Step 5:
Click Update, the process will begin, follow the onscreen prompts as required.
 
 <img width="931" height="583" alt="image" src="https://github.com/user-attachments/assets/ee599392-09e6-4503-8511-bfa15186889c" />

# Step 6:
Once installed you can close NuGet Package manager, next we should confirm the instal took place successfully.

Click on the Server Extension, and over to the right, confirm the new version.

<img width="1838" height="467" alt="image" src="https://github.com/user-attachments/assets/d44c2c53-7630-48db-be88-ba04b4c07b5a" />

# Step 7
Confirm the interface works as required.

Double click the server extension to open it's interface, click the accpet button and ensure "Update successful" shows up in green at the top.

<img width="1250" height="594" alt="image" src="https://github.com/user-attachments/assets/b7911ae8-ed60-471e-8ab9-05c6a21e591f" />





