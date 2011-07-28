using System;
using System.Security.Cryptography;

namespace RavenFS.Util
{
	public class HashKey
	{
		public HashKey(byte[] buffer, int position, int size)
		{
			using (var sha256 = SHA256.Create())
			{
				Strong  = sha256.ComputeHash(buffer, position, size);
				Weak = new RabinKarpHasher(size).Init(buffer, position, size);
			}
		}

		public HashKey()
		{
			
		}

		public byte[] Strong { get; set; }
		public int Weak { get; set; }
	}
}