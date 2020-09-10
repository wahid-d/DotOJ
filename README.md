﻿# Judge1

Yet another online judge.

## Dependencies

Judge1 depends on multiple open-source projects:

- [.NET Core](https://dotnet.microsoft.com/)
- [Ajax.org Cloud9 Editor](https://ace.c9.io/)
- [Angular 10](https://angular.io/)
- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
- [Entity Framework Core](https://github.com/dotnet/efcore)
- [Identity Server](https://identityserver.io/)
- [Node.js](https://nodejs.org/)
- [Ng-Zorro](https://ng.ant.design/)
- [MariaDB](https://mariadb.org/)

## Installation

Application must run on GNU/Linux OS with cgroup enabled. Follow the steps to prepare a dev environment:

### 1. Install .NET Core SDK

Proceed to [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) and download .NET Core SDK for your platform. Do not download .NET Framework or you will not be able to make a build. After installation, fire `dotnet` in a shell to check it is correctly installed.

**Important**: You MIGHT see a line in output that tells 'Successfully installed the ASP.NET Core HTTPS Development Certificate.' This would be critical in step 5 for development environment.

Now install Entiry Framework Core CLI tools with the following commands ([reference](https://docs.microsoft.com/en-us/ef/core/miscellaneous/cli/dotnet)):

```shell
$ dotnet tool install --global dotnet-ef
$ dotnet add package Microsoft.EntityFrameworkCore.Design
```

You can verify that the EFCore tool is correctly installed with `dotnet ef`.

### 2. Install Node.js

Visit (https://nodejs.org/)[https://nodejs.org/] and download the latest LTS version installer. After installation, you should check `node --version` and `npm --version` to make sure these two tools are working.

We are not using Yarn for this project. If your Internet connection is not smooth, refer to [this page](https://developer.aliyun.com/mirror/NPM) to learn how to change registry for NPM.

### 3. Install dependencies

Clone this repository and open the folder in a shell.

- Run the following command to install .NET packages.
  ```shell
  $ dotnet restore
  ```
- `cd` into directory `/ClientApp` and run the following command to install Node.js dependencies.
  ```shell
  $ npm install
  ```
  During the installation you might get a prompt for telemetering of Angular. After that you should see Angular modules being compiled locally. You should test installation with `ng version`.

### 4. Configure Data Source

We are using MySQL or MariaDB (preferred) as the data source. 

Install DB server on your computer and update the connection string in `WebApp/appsettings.json`, then create a user called `judge1` with full access to database `judge1`. Tables will be created on the first run so there is no need for manual migrations.

### 5. Run the application

Before we run the application for the first time, it is CRITICAL to install and trust an HTTPS development certificate on Windows and macOS. Simply run the following command and trust the certificate, or refer to [this manual](https://docs.microsoft.com/en-us/aspnet/core/security/enforcing-ssl):

```shell
$ dotnet dev-certs https --trust
```

Start `WebApp` and `Worker` with `dotnet run` and you should be able to visit the site at `https://localhost:5001`.

## Deployment

### Docker Containers

Docker containers are published in registry and namespace `ccr.ccs.tencentyun.com/judge1`. There are five contianers to build and run services:

- `sdk`: .NET Core SDK.
- `runtime`: ASP.NET runtime.
- `env`: build environment.
- `webapp`: web frontend and server.
- `worker`: judge service.

For more information on how to deploy with docker, refer to [Dockerize/README.md](Dockerize/README.md).

### Scaling Application

Each worker can only send judge requests to one Judge0 backend service, but one Judge0 backend service can accept requests from multiple workers. The application can be easily scaled by adding or removing workers and backend services.

However, workers needs to share file system with web app in order to keep judge data updated. On the other hand, if judge data can be made read only, then workers can run on separate environments with connection to the same DB context.

### Create X.509 Cerfificate

```shell
$ openssl req -x509 -newkey rsa:4096 -sha256 -nodes \
  -subj "/CN=Judge1" -keyout identity.key -out identity.crt
$ openssl pkcs12 -export -out out/identity.pfx -password pass:identity \
  -inkey identity.key -in identity.crt -certfile identity.crt
```
