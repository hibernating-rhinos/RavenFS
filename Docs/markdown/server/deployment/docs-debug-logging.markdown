﻿#Enabling debug logging

RavenFS has extensive support for debug logging, enabling you to figure out exactly what is going on in the server. By default, logging is turned off but you can enable it at any time by creating a file called "NLog.config" in RavenFS's base directory with the following content:

{CODE-START:csharp/}
    <nlog xmlns="http://www.nlog-project.org/schemas/NLog.netfx35.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    	<targets>
    		<target 
    			xsi:type="AsyncWrapper"
    			name="AsyncLog">

    			<target xsi:type="SplitGroup">
    				<target xsi:type="HttpEndpoint" />
    				<target name="File" xsi:type="File"
    								fileName="${basedir}\Logs\${shortdate}.log">
    					<layout xsi:type="CsvLayout">
    						<column name="time" layout="${longdate}" />
    						<column name="logger" layout="${logger}"/>
    						<column name="level" layout="${level}"/>
    						<column name="message" layout="${message}" />
    						<column name="exception" layout="${exception:format=tostring}" />
						</layout>
					</target>
				</target>
			</target>
		</targets>
		<rules>
			<logger name="*" writeTo="AsyncLog"/>
		</rules>
	</nlog>
{CODE-END/}

##Logging endpoint

To get the last logs of the server you can use GET `/logs/[type]` endpoint where `[type]` is an optional argument, its allowed values are: `error` or `warn`.