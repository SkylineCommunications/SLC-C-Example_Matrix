using System;
using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// Writes on Router Control Tables.
	/// </summary>
	/// <param name="protocol">Link with Skyline DataMiner.</param>
	public static void Run(SLProtocol protocol)
	{
		int trigger = protocol.GetTriggerParameter();
		string key = protocol.RowKey();
		string setValue = Convert.ToString(protocol.GetParameter(trigger));
		protocol.SetParameter(Parameter.Write.routercontroloutputsserializedsets_1161, Convert.ToString(trigger) + ";" + key + ";" + setValue);
	}
}