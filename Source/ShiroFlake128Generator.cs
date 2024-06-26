﻿using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security;
using System.Security.Cryptography;
using System.Threading;

namespace ShiroFlake
{
	/// <summary>
	///		Unique id generator for 128-bit data.
	/// </summary>
	public class ShiroFlake128Generator
	{
		#region Member

		private readonly long _CustomOffset = 0;
		private const byte TimeStampBit = 48;
		private const byte MachineBit = 16;
		//private const byte RandomBit = 64;
		private readonly bool _Waiting = false;
		private readonly bool _RngSupplied = false;
		private Flake128 _State;
		private readonly Mutex _Mutex;
		private readonly RandomNumberGenerator _Random;
		private readonly bool _WithOffset;

		#endregion Member

		#region Constructor & Destructor

		/// <summary>
		///		Create an instance of <see cref="ShiroFlake128Generator"/> object.
		/// </summary>
		/// <param name="machineId">
		///		The unique number to identified the server or machine 
		///		that generate the <see cref="ShiroFlake"/> number.
		/// </param>
		/// <param name="offset">
		///		offset for the timestamp in UTC epoch milliseconds. Default to 1577836800000.
		/// </param>
		/// <param name="waiting">
		///		If set <see langword="true"/> the gerator will waiting 
		///		the next timestamp when the sequence number reach the maximum.
		/// </param>
		public ShiroFlake128Generator(uint machineId, RandomNumberGenerator rng = null, long offset = 1577836800000, bool waiting = false)
		{
			if (rng == null)
			{
				this._Random = RandomNumberGenerator.Create();
			}
			else
			{
				this._Random = rng;
				this._RngSupplied = true;
			}

			this._Waiting = waiting;

			if (offset > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
			{
				throw new ArgumentOutOfRangeException("Offset time is bigger than current UTC epoch.");
			}

			if (offset == 0)
			{
				this._WithOffset = false;
			}

			this._CustomOffset = offset;

			if (machineId < 0 || machineId > ((long)1 << MachineBit) - 1)
			{
				throw new ArgumentOutOfRangeException($"The machine id is out of range. Acceptable range is 0 to { ((long)1 << MachineBit) - 1 }.");
			}

			this._Mutex = new Mutex();

			this._State = new Flake128
			{
				_CurrentTime = this.GetMilliseconds(),
				_MachineId = machineId,
				_RandomNumbers = 0
			};
		}

		~ShiroFlake128Generator()
		{
			if (this._RngSupplied == false)
			{
				this._Random.Dispose();
			}

			this._Mutex.Dispose();
		}

		#endregion Constructor & Destructor

		#region Internal Method

		/// <summary>
		///		Get the latest UTC time in milliseconds.
		/// </summary>
		/// <returns>
		///		The latest time from the system.
		/// </returns>
		internal long GetMilliseconds()
		{
			if (this._WithOffset == false)
			{
				return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			}
			return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - this._CustomOffset;
		}

		#endregion Internal Method

		#region Public Method

		/// <summary>
		///		Generate the unique id from this machine.
		/// </summary>
		/// <returns>
		///		A 128-bit integer unique number in array og bytes.
		/// </returns>
		public byte[] NextId()
		{
			this._Mutex.WaitOne();

			var currentTimeStamp = this.GetMilliseconds();

			if (this._State._CurrentTime < currentTimeStamp)
			{
				this._State._CurrentTime = currentTimeStamp;
				var randomNumberBytes = new byte[8];
				this._Random.GetNonZeroBytes(randomNumberBytes);
				this._State._RandomNumbers = BinaryPrimitives.ReadUInt64LittleEndian(randomNumberBytes);
			}
			else // this._State._CurrentTime >= currentTimeStamp
			{
				if ((this._State._RandomNumbers + 1) == ulong.MaxValue)
				{
					// C# can't make a thread to sleep in ticks
					// the solution is to loop until the time is changed
					// or return 0 value.

					if (this._Waiting)
					{
						while (this._State._CurrentTime <= currentTimeStamp)
						{
							this._State._CurrentTime = this.GetMilliseconds();
						}

						var randomNumberBytes = new byte[8];
						this._Random.GetNonZeroBytes(randomNumberBytes);
						this._State._RandomNumbers = BitConverter.ToUInt64(randomNumberBytes, 0);
					}
					else
					{
						return null;
					}
				}
			}

			var id = new List<byte>();

#if NET5_0_OR_GREATER
			var temp = new Span<byte>();
			var timestampWithMachineId = (ulong)this._State._CurrentTime << MachineBit | this._State._MachineId;
			System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(temp, timestampWithMachineId);

			temp.Slice(8);

			System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(temp, this._State._RandomNumbers);
			id.AddRange(temp.ToArray());
#else
			if (BitConverter.IsLittleEndian)
			{
				var timestampWithMachineId = (ulong)this._State._CurrentTime << MachineBit | this._State._MachineId;
				var reversetimestampWithMachineId = BitConverter.GetBytes(timestampWithMachineId);
				Array.Reverse(reversetimestampWithMachineId);
				id.AddRange(reversetimestampWithMachineId);

				var reverseRandomNumbers = BitConverter.GetBytes(this._State._RandomNumbers);
				Array.Reverse(reverseRandomNumbers);
				id.AddRange(reverseRandomNumbers);
			}
			else
			{
				var timestampWithMachineId = this._State._CurrentTime << MachineBit | this._State._MachineId;
				id.AddRange(BitConverter.GetBytes(timestampWithMachineId));
				id.AddRange(BitConverter.GetBytes(this._State._RandomNumbers));
			}
#endif
			this._Mutex.ReleaseMutex();

			return id.ToArray();
		}

//		/// <summary>
//		///		Generate the unique id from this machine.
//		/// </summary>
//		/// <remarks>
//		///		Remove unnecessary check and locks.
//		/// </remarks>
//		/// <returns>
//		///		A 128-bit integer unique number in array og bytes.
//		/// </returns>
//		public byte[] FastNextId()
//		{
//			var currentTimeStamp = this.GetMilliseconds();

//			if (currentTimeStamp >= ((long)1 << TimeStampBit) - 1)
//			{
//				throw new Exception("Timestamp was over the limit and can not generate new timestamp.");
//			}

//			if (this._State._CurrentTime < currentTimeStamp)
//			{
//				this._State._CurrentTime = currentTimeStamp;
//				var randomNumberBytes = new byte[8];
//				this._Random.GetNonZeroBytes(randomNumberBytes);
//				this._State._RandomNumbers = BitConverter.ToUInt64(randomNumberBytes, 0);
//			}
//			else // this._State._CurrentTime >= currentTimeStamp
//			{
//				if ((this._State._RandomNumbers + 1) == ulong.MaxValue)
//				{
//					// C# can't make a thread to sleep in ticks
//					// the solution is to loop until the time is changed
//					// or return 0 value.

//					if (this._Waiting)
//					{
//						while (this._State._CurrentTime <= currentTimeStamp)
//						{
//							this._State._CurrentTime = this.GetMilliseconds();
//						}

//						var randomNumberBytes = new byte[8];
//						this._Random.GetNonZeroBytes(randomNumberBytes);
//						this._State._RandomNumbers = BitConverter.ToUInt64(randomNumberBytes, 0);
//					}
//					else
//					{
//						return null;
//					}
//				}
//			}

//#if NET5_0_OR_GREATER
//			var id = new Span<byte>();

//			var timestampWithMachineId = (ulong)this._State._CurrentTime << MachineBit | this._State._MachineId;
//			System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(id, timestampWithMachineId);

//			id.Slice(8);

//			System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(id, this._State._RandomNumbers);

//			return id.ToArray();
//#else
//			var id = new List<byte>();

//			if (BitConverter.IsLittleEndian)
//			{
//				var timestampWithMachineId = (ulong)this._State._CurrentTime << MachineBit | this._State._MachineId;
//				var reversetimestampWithMachineId = BitConverter.GetBytes(timestampWithMachineId);
//				Array.Reverse(reversetimestampWithMachineId);
//				id.AddRange(reversetimestampWithMachineId);

//				var reverseRandomNumbers = BitConverter.GetBytes(this._State._RandomNumbers);
//				Array.Reverse(reverseRandomNumbers);
//				id.AddRange(reverseRandomNumbers);
//			}
//			else
//			{
//				var timestampWithMachineId = this._State._CurrentTime << MachineBit | this._State._MachineId;
//				id.AddRange(BitConverter.GetBytes(timestampWithMachineId));
//				id.AddRange(BitConverter.GetBytes(this._State._RandomNumbers));
//			}
//			return id.ToArray();
//#endif
//		}

		#endregion Public Method
	}
}
