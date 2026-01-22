namespace Skyline.Protocol.Interfaces
{
	public static class BandwidthHelper
	{
		/// <summary>
		/// Calculates bandwidth utilization based on the input or output <paramref name="rate"/> and <paramref name="interfaceSpeed"/>.
		/// </summary>
		/// <param name="rate">The input or output rate of the interface. The unit should be consistent to the <paramref name="interfaceSpeed"/>.</param>
		/// <param name="interfaceSpeed">The speed of the interface. The unit should be consistent to the <paramref name="rate"/>.</param>
		/// <returns>The directional (Input or Output) utilization in percent.</returns>
		public static double CalculateUtilization(double rate, double interfaceSpeed)
		{
			if (rate < 0.0 || interfaceSpeed <= 0.0)
			{
				return -1.0;
			}

			return rate * 100.0 / interfaceSpeed;
		}
	}
}