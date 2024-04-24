using System;
using System.Threading;

namespace ShiroFlake
{
	/// <summary>
	///		Unique id generator for 64-bit integer.
	/// </summary>
	public class ShiroFlakeGenerator
	{
		#region Member

		private readonly uint _Mask = 0;
		private readonly byte _MaxBit = 63;
		private readonly bool _Waiting = false;
		private readonly ShiroFlakeConfig _Config;
		private Flake _State;
		private readonly Mutex _Mutex;
		private readonly bool _WithOffset = true;

		#endregion Member

		#region Constructor & Destructor

		/// <summary>
		///		Create an instance of <see cref="ShiroFlakeGenerator"/> object.
		/// </summary>
		/// <param name="machineId">
		///		The unique number to identified the server or machine 
		///		that generate the <see cref="ShiroFlake"/> number.
		/// </param>
		/// <param name="config">
		///		Custom configuration to use generate the <see cref="ShiroFlake"/> number.
		/// </param>
		/// <param name="waiting">
		///		If set <see langword="true"/> the generator will waiting 
		///		the next timestamp when the sequence number reach the maximum.
		/// </param>
		/// <param name="unsigned">
		///		If set <see langword= "true" /> remove signed bit from the id..
		/// </param>
		public ShiroFlakeGenerator(uint machineId, ShiroFlakeConfig config = null, bool waiting = false, bool unsigned = false)
		{
			this._Waiting = waiting;

			if (unsigned)
			{
				this._MaxBit = 64;
			}

			if (config == null)
			{
				this._Config = new ShiroFlakeConfig();
			}
			else
			{
				var usedBit = config.TimeStampBit + config.MachineBit + config.SequenceBit;
				if (usedBit != this._MaxBit)
				{
					throw new ArgumentException($"The configuration is invalid, it use { usedBit } bit but ShiroFlake only accept { this._MaxBit }.");
				}

				if (config.CustomOffset > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
				{
					throw new ArgumentOutOfRangeException("Offset time is bigger than current UTC epoch.");
				}
				this._Config = config;

				if (this._Config.CustomOffset == 0)
				{
					this._WithOffset = false;
				}
			}

			if (machineId < 0 || machineId > (1 << this._Config.MachineBit) - 1)
			{
				throw new ArgumentOutOfRangeException($"The machine id is out of range. Acceptable range is 0 to { (1 << config.MachineBit) - 1 }.");
			}

			this._Config.TimeStampBit = (byte)(this._MaxBit - this._Config.TimeStampBit);
			this._Config.MachineBit = (byte)(this._Config.TimeStampBit - this._Config.MachineBit);

			this._Mask = (uint)((1 << this._Config.SequenceBit) - 1);

			this._Mutex = new Mutex();

			this._State = new Flake
			{
				_CurrentTime = this.GetMiliseconds(),
				_MachineId = machineId,
				_Sequence = 0
			};
		}

		~ShiroFlakeGenerator()
		{
			this._Mutex.Dispose();
		}

		#endregion Constructor & Destructor

		#region Internal Method

		/// <summary>
		///		Get the latest UTC time in miliseconds.
		/// </summary>
		/// <returns>
		///		The latest time from the system.
		/// </returns>
		internal long GetMiliseconds()
		{
			if (this._WithOffset == false)
			{
				return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

			return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - this._Config.CustomOffset;
		}

		#endregion Internal Method

		#region Public Method

		/// <summary>
		///		Generate the unique id from this machine.
		/// </summary>
		/// <returns>
		///		Signed 64-bit integer unique number.
		/// </returns>
		public long NextId()
		{
			this._Mutex.WaitOne();

			var currentTimeStamp = this.GetMiliseconds();

			if (this._State._CurrentTime < currentTimeStamp)
			{
				this._State._CurrentTime = currentTimeStamp;
				this._State._Sequence = 0;
			}
			else // this._State._CurrentTime >= currentTimeStamp
			{
				if (((this._State._Sequence + 1) & this._Mask) == 0)
				{
                    // C# can't make a thread to sleep in ticks
                    // the solution is to loop until the time is changed
                    // or return 0 value.

                    Console.WriteLine("masking sequence");
                    if (this._Waiting)
					{
						while (this._State._CurrentTime <= currentTimeStamp)
						{
							this._State._CurrentTime = this.GetMiliseconds();
						}
					}
					else
					{
						return 0;
					}
				}
				this._State._Sequence = (this._State._Sequence + 1) & this._Mask;
			}

			var id = this._State._CurrentTime << this._Config.TimeStampBit
				| (long)(this._State._MachineId << this._Config.MachineBit)
				| (long)this._State._Sequence;

			this._Mutex.ReleaseMutex();

			return id;
		}

		/// <summary>
		///		Generate the unique id from this machine.
		/// </summary>
		/// <returns>
		///		Unsigned 64-bit integer unique number.
		/// </returns>
		public ulong NextUnsignedId()
		{
			this._Mutex.WaitOne();

			var currentTimeStamp = this.GetMiliseconds();

			if (this._State._CurrentTime < currentTimeStamp)
			{
				this._State._CurrentTime = currentTimeStamp;
				this._State._Sequence = 0;

				Console.WriteLine("time:" + new DateTime(this._State._CurrentTime * 10000));
			}
			else // this._State._CurrentTime >= currentTimeStamp
			{
				if (((this._State._Sequence + 1) & this._Mask) == 0)
				{
					// C# can't make a thread to sleep in ticks
					// the solution is to loop until the time is changed
					// or return 0 value.

					if (this._Waiting)
					{
						while (this._State._CurrentTime <= currentTimeStamp)
						{
							this._State._CurrentTime = this.GetMiliseconds();
						}
					}
					else
					{
						return 0;
					}
				}
				this._State._Sequence = (this._State._Sequence + 1) & this._Mask;
			}
			var id = ((ulong)this._State._CurrentTime << this._Config.TimeStampBit)
				| (ulong)(this._State._MachineId << this._Config.MachineBit)
				| (ulong)this._State._Sequence;

			this._Mutex.ReleaseMutex();

			return id;
		}

		#endregion Public Method
	}
}
