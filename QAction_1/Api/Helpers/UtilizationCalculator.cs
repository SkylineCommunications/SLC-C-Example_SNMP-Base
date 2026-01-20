namespace Skyline.Protocol.Api.Helpers
{
	public static class UtilizationCalculator
	{
		/// <summary>
		/// Calculates either TX or RX utilization.
		/// </summary>
		/// <param name="rate">The input/output rate of the interface. The unit should be consistent to the <paramref name="interfaceSpeed"/>.</param>
		/// <param name="interfaceSpeed">The speed of the interface. The unit should be consistent to the <paramref name="rate"/>.</param>
		/// <returns>The directional utilization in percent.</returns>
		public static double CalculateDirectionalUtilization(double rate, double interfaceSpeed)
		{
			if (rate < 0.0 || interfaceSpeed <= 0.0)
				return -1.0;

			return rate * 100.0 / interfaceSpeed;
		}
	}
}