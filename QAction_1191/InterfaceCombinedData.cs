using Skyline.DataMiner.Scripting;

public class InterfaceCombinedData
{
	public InterfaceCombinedData(
		InterfacesQActionRow interfacesStateTableRow,
		InterfacesdetailsrxQActionRow interfaceDetailsRxRow,
		InterfacesdetailstxQActionRow interfaceDetailsTxRow)
	{
		InterfacesRow = interfacesStateTableRow;
		InterfacesRxRow = interfaceDetailsRxRow;
		InterfacesTxRow = interfaceDetailsTxRow;
	}

	public InterfacesQActionRow InterfacesRow { get; set; }

	public InterfacesdetailsrxQActionRow InterfacesRxRow { get; set; }

	public InterfacesdetailstxQActionRow InterfacesTxRow { get; set; }
}