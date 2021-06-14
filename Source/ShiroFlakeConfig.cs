namespace ShiroFlake
{
	/// <summary>
	///		<see cref="ShiroFlake"/> custom config.
	/// </summary>
	public class ShiroFlakeConfig
	{
		/// <summary>
		///		Your offset for the timestamp in UTC epoch miliseconds. 
		/// </summary>
		/// <remarks>
		///		If not set, it will use the default offset which is the first second in 2020 
		///		(01 January 2020 00:00:00).
		/// </remarks>
		public long CustomOffset { set; get; } = 1577836800000;

		/// <summary>
		///		Number of many bit the time stamp can use.
		/// </summary>
		/// <remarks>
		///		By default is use 41 bit for signed 64-bit integer. 
		/// </remarks>
		public byte TimeStampBit { set; get; } = 41;

		/// <summary>
		///		Number of many bit the machine or server unique Id can use.
		/// </summary>
		/// <remarks>
		///		By default is 12 bit.
		/// </remarks>
		public byte MachineBit { set; get; } = 12;

		/// <summary>
		///		Number of many bit the incremental sequence can use.
		/// </summary>
		/// <remarks>
		///		By default is 10 bit.
		/// </remarks>
		public byte SequenceBit { set; get; } = 10;
	}
}
