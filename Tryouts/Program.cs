using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using RavenFS.Client;
using System.Threading.Tasks;

namespace Tryouts
{
	class Program
	{
		static void Main(string[] args)
		{
			var multiPartParser = new MultiPartParser("multipart/form-data; boundary=----------ei4ei4KM7KM7ei4gL6cH2Ij5GI3Ij5",
			                                          new MemoryStream(Encoding.UTF8.GetBytes(a)), Encoding.UTF8);

			var tuple = multiPartParser.Next();
			foreach (string key in tuple.Item2)
			{
				Console.WriteLine("{0}: {1}", key, tuple.Item2[key]);
			}
			Console.WriteLine(new StreamReader(tuple.Item1).ReadToEnd());
		}

		const string a =
				@"------------ei4ei4KM7KM7ei4gL6cH2Ij5GI3Ij5
Content-Disposition: form-data; name=""Filename""

ConsoleApplication3.csproj
------------ei4ei4KM7KM7ei4gL6cH2Ij5GI3Ij5
Content-Disposition: form-data; name=""SID""

d72b47c8261b78a6c4643eddbef2fbcc
------------ei4ei4KM7KM7ei4gL6cH2Ij5GI3Ij5
Content-Disposition: form-data; name=""file""; filename=""ConsoleApplication3.csproj""
Content-Type: application/octet-stream

?<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">x86</Platform>
    <ProductVersion>8.0.30703</ProductVersion>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectGuid>{1D4EC12D-ED72-4978-B77F-EEC289C5FBC2}</ProjectGuid>
    <OutputType>Exe</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>ConsoleApplication3</RootNamespace>
    <AssemblyName>ConsoleApplication3</AssemblyName>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
    <TargetFrameworkProfile>Client</TargetFrameworkProfile>
    <FileAlignment>512</FileAlignment>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|x86' "">
    <PlatformTarget>x86</PlatformTarget>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|x86' "">
    <PlatformTarget>x86</PlatformTarget>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""Lucene.Net"">
      <HintPath>..\packages\Lucene.2.9.2.2\lib\Lucene.Net.dll</HintPath>
    </Reference>
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.Xml.Linq"" />
    <Reference Include=""System.Data.DataSetExtensions"" />
    <Reference Include=""Microsoft.CSharp"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Xml"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Program.cs"" />
    <Compile Include=""Properties\AssemblyInfo.cs"" />
  </ItemGroup>
  <ItemGroup>
    <None Include=""packages.config"" />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name=""BeforeBuild"">
  </Target>
  <Target Name=""AfterBuild"">
  </Target>
  -->
</Project>
------------ei4ei4KM7KM7ei4gL6cH2Ij5GI3Ij5
Content-Disposition: form-data; name=""Upload""

Submit Query
------------ei4ei4KM7KM7ei4gL6cH2Ij5GI3Ij5--";
	}
}
