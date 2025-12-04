using Skyline.DataMiner.Scripting;

public class InterfaceTablesRowData
{
	public InterfaceTablesRowData(
		InterfacesQActionRow interfacesStateTableRow,
		InterfacedetailsrxQActionRow interfaceDetailsRxRow,
		InterfacedetailstxQActionRow interfaceDetailsTxRow)
	{
		InterfacesRow = interfacesStateTableRow;
		InterfaceDetailsRxRow = interfaceDetailsRxRow;
		InterfaceDetailsTxRow = interfaceDetailsTxRow;
	}

	public InterfacesQActionRow InterfacesRow { get; set; }

	public InterfacedetailsrxQActionRow InterfaceDetailsRxRow { get; set; }

	public InterfacedetailstxQActionRow InterfaceDetailsTxRow { get; set; }
}