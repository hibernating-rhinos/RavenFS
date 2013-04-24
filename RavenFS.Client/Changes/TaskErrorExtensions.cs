// -----------------------------------------------------------------------
//  <copyright file="TaskErrorExtensions.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace RavenFS.Client.Changes
{
	internal static class TaskErrorExtensions
	{
		 public static async Task ObserveException(this Task self)
		 {
			 // this merely observe the exception task, nothing else
			 try
			 {
				 await self;
			 }
			 catch (Exception e)
			 {
				 GC.KeepAlive(e);
			 }
		 }
	}
}