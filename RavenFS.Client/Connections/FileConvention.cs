//-----------------------------------------------------------------------
// <copyright file="DocumentConvention.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;

namespace RavenFS.Client.Connections
{
	/// <summary>
	/// The set of conventions used by the <see cref="FileConvention"/> which allow the users to customize
	/// the way the Raven client API behaves
	/// </summary>
	public class FileConvention
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FileConvention"/> class.
		/// </summary>
		public FileConvention()
		{
			MaxFailoverCheckPeriod = TimeSpan.FromMinutes(5);
			FailoverBehavior = FailoverBehavior.AllowReadsFromSecondaries;
		}

		/// <summary>
		/// How should we behave in a replicated environment when we can't 
		/// reach the primary node and need to failover to secondary node(s).
		/// </summary>
		public FailoverBehavior FailoverBehavior { get; set; }

		/// <summary>
		/// Clone the current conventions to a new instance
		/// </summary>
		public FileConvention Clone()
		{
			return (FileConvention) MemberwiseClone();
		}

		public FailoverBehavior FailoverBehaviorWithoutFlags
		{
			get { return FailoverBehavior & (~FailoverBehavior.ReadFromAllServers); }
		}

		/// <summary>
		/// The maximum amount of time that we will wait before checking
		/// that a failed node is still up or not.
		/// Default: 5 minutes
		/// </summary>
		public TimeSpan MaxFailoverCheckPeriod { get; set; }

		/// <summary>
		/// Enable multipule async operations
		/// </summary>
		public bool AllowMultipuleAsyncOperations { get; set; }
	}
}
