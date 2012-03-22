using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RavenFS.Infrastructure;
using RavenFS.Rdc.Wrapper;

namespace RavenFS.Rdc
{
	public static class NeedListParser
	{
		public static Task ParseAsync(IPartialDataAccess source, IPartialDataAccess seed, Stream output,
		                              IList<RdcNeed> needList, int position = 0)
		{
			if (position >= needList.Count)
			{
				return new CompletedTask();
			}
			var item = needList[position];
			Task task;

			switch (item.BlockType)
			{
				case RdcNeedType.Source:
					task = source.CopyToAsync(output, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength));
					break;
				case RdcNeedType.Seed:
					task = seed.CopyToAsync(output, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength));
					break;
				default:
					throw new NotSupportedException();
			}

			return task.ContinueWith(resultTask =>
			{
				if (resultTask.Status == TaskStatus.Faulted)
					resultTask.Wait(); // throws
				return ParseAsync(source, seed, output, needList, position + 1);
			}).Unwrap();
		}
	}
}