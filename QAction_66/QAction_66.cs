using System;
using System.Collections.Generic;
using System.Linq;

using Skyline.DataMiner.Core.Matrix.Protocol;
using Skyline.DataMiner.Scripting;

public static class ParameterDiscreetValues
{
	public enum ViewPageControl
	{
		Show = 1,
		Hide = 2
	}

	public enum LockedValues
	{
		Locked = 1,
		Unlocked = 2
	}
}

public class QAction
{
	private readonly MatrixStorage _matrixStorage = new MatrixStorage();

	/// <summary>
	/// Matrix Modifications.
	/// </summary>
	/// <param name="protocol">Link with Skyline DataMiner.</param>
	public void Run(SLProtocol protocol)
	{
		int triggerParameter = protocol.GetTriggerParameter();
		try
		{
			switch (triggerParameter)
			{
				case Parameter.Write.matrix_101:
				case 66:    // DiscreetInfo
				case Parameter.Write.routercontroloutputsvirtualsets_1160:
				case Parameter.Write.routercontroloutputsserializedsets_1161:
					_matrixStorage.GetMatrix(protocol).ProcessParameterSetFromUI(protocol, triggerParameter);
					break;
				case Parameter.Routersysleveloutputs.tablePid:	// table 1200
					ProcessOutputData(protocol);
					break;
				case Parameter.Routersyslevelinputs.tablePid:	// table 1300
					ProcessInputData(protocol);
					break;
				case Parameter.routersysleveltakenextwritebufferitem_1404:
					TakeNextWriteBufferItem(protocol);
					break;
				case Parameter.matrixsettings_displaytype_124:
					ChangeDisplayType(protocol);
					break;
				default:
					return; // Not supported trigger parameter.
			}
		}
		catch (Exception ex)
		{
			protocol.Log("QA" + protocol.QActionID + "|" + Convert.ToString(triggerParameter) + "|Run|Exception " + Convert.ToString(ex), LogType.Error, LogLevel.NoLogging);
		}
	}

	private static bool TryGetConnectedInput(SLProtocol protocol, int displayedInputs, object connectedInput, int output, out int input)
	{
		if (!Int32.TryParse(Convert.ToString(connectedInput), out input))
		{
			protocol.Log("QA" + protocol.QActionID + "|Output index: " + Convert.ToString(output + 1) + " is connected to an input that can't be converted to a number: " + Convert.ToString(connectedInput), LogType.Error, LogLevel.NoLogging);
			return false;
		}

		if (input < 0 && input > displayedInputs)
		{
			protocol.Log("QA" + protocol.QActionID + "|Output index: " + Convert.ToString(output + 1) + " is connected to an input that is out of range: " + Convert.ToString(connectedInput) + " maximum known input is " + Convert.ToString(displayedInputs), LogType.Error, LogLevel.NoLogging);
			return false;
		}

		input--;
		return true;
	}

	private static bool TryGetLock(SLProtocol protocol, object lockedValue, int index, bool isInput, out bool isLocked)
	{
		isLocked = false;
		int lockType;
		if (!Int32.TryParse(Convert.ToString(lockedValue), out lockType))
		{
			protocol.Log("QA" + protocol.QActionID + "|Provided locked value " + Convert.ToString(lockedValue) + " can't be converted to a number for " + (isInput ? "input " : "output ") + Convert.ToString(index + 1), LogType.Error, LogLevel.NoLogging);
			return false;
		}

		if (lockType == (int)ParameterDiscreetValues.LockedValues.Locked)
		{
			isLocked = true;
			return true;
		}
		else if (lockType == (int)ParameterDiscreetValues.LockedValues.Unlocked)
		{
			return true;
		}
		else
		{
			protocol.Log("QA" + protocol.QActionID + "|Unknown output locked value: " + Convert.ToString(lockType) + " for " + (isInput ? "input " : "output ") + Convert.ToString(index + 1), LogType.Error, LogLevel.NoLogging);
			return false;
		}
	}

	private static MatrixDisplayType SetForMatrix(SLProtocol protocol, bool isDisplayedMatrix, bool isDisplayedTables)
	{
		if (!isDisplayedMatrix && isDisplayedTables)
		{
			protocol.SetParameters(new[] { Parameter.matrixviewpagecontrol_5, Parameter.tableviewpagecontrol_6 }, new object[] { (int)ParameterDiscreetValues.ViewPageControl.Show, (int)ParameterDiscreetValues.ViewPageControl.Hide });
		}
		else if (!isDisplayedMatrix)
		{
			protocol.SetParameter(Parameter.matrixviewpagecontrol_5, (int)ParameterDiscreetValues.ViewPageControl.Show);
		}
		else if (isDisplayedTables)
		{
			protocol.SetParameter(Parameter.tableviewpagecontrol_6, (int)ParameterDiscreetValues.ViewPageControl.Hide);
		}
		else
		{
			// Do nothing
		}

		return MatrixDisplayType.Matrix;
	}

	private static MatrixDisplayType SetForTables(SLProtocol protocol, bool isDisplayedMatrix, bool isDisplayedTables)
	{
		if (isDisplayedMatrix && !isDisplayedTables)
		{
			protocol.SetParameters(new[] { Parameter.matrixviewpagecontrol_5, Parameter.tableviewpagecontrol_6 }, new object[] { (int)ParameterDiscreetValues.ViewPageControl.Hide, (int)ParameterDiscreetValues.ViewPageControl.Show });
		}
		else if (isDisplayedMatrix)
		{
			protocol.SetParameter(Parameter.matrixviewpagecontrol_5, (int)ParameterDiscreetValues.ViewPageControl.Hide);
		}
		else if (!isDisplayedTables)
		{
			protocol.SetParameter(Parameter.tableviewpagecontrol_6, (int)ParameterDiscreetValues.ViewPageControl.Show);
		}
		else
		{
			// Do nothing
		}

		return MatrixDisplayType.Tables;
	}

	private static MatrixDisplayType SetForMatrixAndTables(SLProtocol protocol, bool isDisplayedMatrix, bool isDisplayedTables)
	{
		if (!isDisplayedMatrix && !isDisplayedTables)
		{
			protocol.SetParameters(new[] { Parameter.matrixviewpagecontrol_5, Parameter.tableviewpagecontrol_6 }, new object[] { (int)ParameterDiscreetValues.ViewPageControl.Show, (int)ParameterDiscreetValues.ViewPageControl.Show });
		}
		else if (!isDisplayedMatrix)
		{
			protocol.SetParameter(Parameter.matrixviewpagecontrol_5, (int)ParameterDiscreetValues.ViewPageControl.Show);
		}
		else if (!isDisplayedTables)
		{
			protocol.SetParameter(Parameter.tableviewpagecontrol_6, (int)ParameterDiscreetValues.ViewPageControl.Show);
		}
		else
		{
			// Do nothing
		}

		return MatrixDisplayType.MatrixAndTables;
	}

	private static bool CheckValidColumns(object[] columns)
	{
		for (int i = 0; i < columns.Length; i++)
		{
			if (columns[i] == null)
			{
				return false;
			}

			if (i == 0)
			{
				continue;
			}

			object[] previousCol = (object[])columns[i - 1];
			object[] currentCol = (object[])columns[i];
			if (previousCol.Length != currentCol.Length)
			{
				return false;
			}
		}

		return true;
	}

	private static bool CheckValidTable(object[] columns, int expectedSize)
	{
		if (columns == null || columns.Length < expectedSize)
		{
			return false;
		}

		return CheckValidColumns(columns);
	}

	private static bool ValidateInstance(SLProtocol protocol, int maxAllowed, object instance, string busNumber, bool isInput, ref int maximumFoundItems, out int index)
	{
		index = -1;
		string instanceValue = Convert.ToString(instance);
		if (!instanceValue.StartsWith(busNumber))
		{
			return false;
		}

		string type = isInput ? "input" : "output";
		instanceValue = instanceValue.Replace(busNumber, String.Empty);
		if (!Int32.TryParse(instanceValue, out index))
		{
			protocol.Log("QA" + protocol.QActionID + "|PK column of the " + type + " table can't be converted to a number. Index: " + instanceValue, LogType.Error, LogLevel.NoLogging);
			return false;
		}

		if (index <= 0 || index > maxAllowed)
		{
			protocol.Log("QA" + protocol.QActionID + "|" + type + "index: " + Convert.ToString(index) + " is out of range, maximum value is " + Convert.ToString(maxAllowed), LogType.Error, LogLevel.NoLogging);
			return false;
		}

		if (index > maximumFoundItems)
		{
			maximumFoundItems = index;
		}

		index--;
		return true;
	}

	private void ProcessInputData(SLProtocol protocol)
	{
		try
		{
			uint[] idx = new uint[3];
			idx[0] = Parameter.Routersyslevelinputs.Idx.routersyslevelinputsinstance_1301;
			idx[1] = Parameter.Routersyslevelinputs.Idx.routersyslevelinputsname_1302;
			idx[2] = Parameter.Routersyslevelinputs.Idx.routersyslevelinputslocked_1303;
			object[] tableCols = (object[])protocol.NotifyProtocol((int)Skyline.DataMiner.Net.Messages.NotifyType.NT_GET_TABLE_COLUMNS, Parameter.Routersyslevelinputs.tablePid, idx);    // table 1300, notify 321
			if (!CheckValidTable(tableCols, idx.Length))
			{
				protocol.Log("QA" + protocol.QActionID + "|The columns of input table are null or don't have an equal size", LogType.Error, LogLevel.NoLogging);
				return;
			}

			object[] indexCol = (object[])tableCols[0];
			object[] inputName = (object[])tableCols[1];
			object[] inputLocked = (object[])tableCols[2];
			Matrix matrix = SetupRouter(protocol);
			int maximumFoundInputs = 0;
			for (int i = 0; i < indexCol.Length; i++)
			{
				int index;
				bool isLocked;
				if (!ValidateInstance(protocol, matrix.MaxInputs, indexCol[i], matrix.BusNumber, true, ref maximumFoundInputs, out index) || !TryGetLock(protocol, inputLocked[i], index, true, out isLocked))
				{
					continue;
				}

				matrix.Inputs[index].Label = Convert.ToString(inputName[i]);
				matrix.Inputs[index].IsLocked = isLocked;
			}

			if (maximumFoundInputs > 0)
			{
				matrix.DisplayedInputs = maximumFoundInputs;
				matrix.ApplyChanges(protocol);
			}
		}
		catch (Exception ex)
		{
			protocol.Log("QA" + protocol.QActionID + "|Exception when processing input table " + Convert.ToString(ex), LogType.Error, LogLevel.NoLogging);
		}
	}

	private void ProcessOutputData(SLProtocol protocol)
	{
		try
		{
			uint[] idx = new uint[4];
			idx[0] = Parameter.Routersysleveloutputs.Idx.routersysleveloutputsinstance_1201;
			idx[1] = Parameter.Routersysleveloutputs.Idx.routersysleveloutputsname_1202;
			idx[2] = Parameter.Routersysleveloutputs.Idx.routersysleveloutputslocked_1203;
			idx[3] = Parameter.Routersysleveloutputs.Idx.routersysleveloutputsinputstatus_1204;
			object[] tableCols = (object[])protocol.NotifyProtocol((int)Skyline.DataMiner.Net.Messages.NotifyType.NT_GET_TABLE_COLUMNS, Parameter.Routersysleveloutputs.tablePid, idx);    // table 1200, notify 321
			if (!CheckValidTable(tableCols, idx.Length))
			{
				protocol.Log("QA" + protocol.QActionID + "|The columns of output table are null or don't have an equal size", LogType.Error, LogLevel.NoLogging);
				return;
			}

			object[] indexCol = (object[])tableCols[0];
			object[] outputName = (object[])tableCols[1];
			object[] outputLocked = (object[])tableCols[2];
			object[] connectedInputCol = (object[])tableCols[3];
			Matrix matrix = SetupRouter(protocol);
			int maximumFoundOutputs = 0;
			for (int i = 0; i < indexCol.Length; i++)
			{
				int output;
				int connectedInput;
				bool isLocked;
				if (!ValidateInstance(protocol, matrix.MaxOutputs, indexCol[i], matrix.BusNumber, false, ref maximumFoundOutputs, out output) || !TryGetLock(protocol, outputLocked[i], output, false, out isLocked) || !TryGetConnectedInput(protocol, matrix.DisplayedInputs, connectedInputCol[i], output, out connectedInput))
				{
					continue;
				}

				matrix.Outputs[output].Label = Convert.ToString(outputName[i]);
				matrix.Outputs[output].IsLocked = isLocked;
				if (connectedInput == -1)
				{
					matrix.Outputs[output].DisconnectAll();
				}
				else
				{
					matrix.Outputs[output].Connect(connectedInput);
				}
			}

			if (maximumFoundOutputs > 0)
			{
				matrix.DisplayedOutputs = maximumFoundOutputs;
				matrix.ApplyChanges(protocol);
			}
		}
		catch (Exception ex)
		{
			protocol.Log("QA" + protocol.QActionID + "|Exception when processing output table " + Convert.ToString(ex), LogType.Error, LogLevel.NoLogging);
		}
	}

	private void ChangeDisplayType(SLProtocol protocol)
	{
		Matrix matrix = SetupRouter(protocol);
		matrix.ApplyChanges(protocol);
	}

	private void TakeNextWriteBufferItem(SLProtocol protocol)
	{
		try
		{
			object[] readParameters = (object[])protocol.GetParameters(new uint[] { Parameter.routersyslevelwritebuffer_1400, Parameter.routersyslevelwriteoid_1401, Parameter.routersyslevelreadvalue_1403 });
			string currentBuffer = Convert.ToString(readParameters[0]);
			ProcessPolledBufferItem(protocol, readParameters[1], readParameters[2]);
			int pos = currentBuffer.IndexOf(";");
			if (pos == -1)
			{
				protocol.SetParameter(Parameter.routersyslevelwritebuffer_1400, String.Empty);
				return;
			}

			currentBuffer = currentBuffer.Substring(pos + 1);
			pos = currentBuffer.IndexOf(";");
			string nextValue = pos == -1 ? currentBuffer : currentBuffer.Substring(0, pos);
			pos = nextValue.IndexOf(",");
			if (pos == -1)
			{
				protocol.Log("QA" + protocol.QActionID + "|Error when processing buffer write, cannot find ',', buffer content is " + currentBuffer, LogType.Error, LogLevel.NoLogging);
				protocol.SetParameter(Parameter.routersyslevelwritebuffer_1400, String.Empty);
				return;
			}

			string oidValue = nextValue.Substring(0, pos);
			nextValue = nextValue.Substring(pos + 1);
			pos = nextValue.IndexOf(",");
			if (pos == -1)
			{
				protocol.Log("QA" + protocol.QActionID + "|Error when processing buffer write, cannot find second ',', buffer content is " + currentBuffer, LogType.Error, LogLevel.NoLogging);
				protocol.SetParameter(Parameter.routersyslevelwritebuffer_1400, String.Empty);
				return;
			}

			bool isInteger = nextValue.Substring(0, pos) == "1";
			string setValue = nextValue.Substring(pos + 1);
			Dictionary<int, object> setParameter = new Dictionary<int, object>();
			setParameter[Parameter.routersyslevelwritebuffer_1400] = currentBuffer;
			setParameter[Parameter.routersyslevelwriteoid_1401] = oidValue;
			if (isInteger)
			{
				setParameter[Parameter.Write.routersyslevelwritevalueinteger_1405] = setValue;
			}
			else
			{
				setParameter[Parameter.Write.routersyslevelwritevalue_1402] = setValue;
			}

			protocol.SetParameters(setParameter.Keys.ToArray(), setParameter.Values.ToArray());
		}
		catch (Exception ex)
		{
			protocol.Log("QA" + protocol.QActionID + "|Exception when processing buffer write " + Convert.ToString(ex), LogType.Error, LogLevel.NoLogging);
		}
	}

	private Matrix SetupRouter(SLProtocol protocol)
	{
		object[] parameters = (object[])protocol.GetParameters(new uint[] { Parameter.matrixviewpagecontrol_5, Parameter.tableviewpagecontrol_6, Parameter.matrixsettings_displaytype_124 });
		bool isDisplayedMatrix = Convert.ToInt32(parameters[0]) == (int)ParameterDiscreetValues.ViewPageControl.Show;
		bool isDisplayedTables = Convert.ToInt32(parameters[1]) == (int)ParameterDiscreetValues.ViewPageControl.Show;
		MatrixDisplayType displayType;
		switch (Convert.ToString(parameters[2]))
		{
			case "1":
				displayType = SetForMatrix(protocol, isDisplayedMatrix, isDisplayedTables);
				break;
			case "2":
				displayType = SetForTables(protocol, isDisplayedMatrix, isDisplayedTables);
				break;
			default:
				displayType = SetForMatrixAndTables(protocol, isDisplayedMatrix, isDisplayedTables);
				break;
		}

		Matrix matrix = _matrixStorage.GetMatrix(protocol);
		matrix.DisplayType = displayType;
		return matrix;
	}

	private void ProcessPolledBufferItem(SLProtocol protocol, object oid, object polledValue)
	{
		string oidValue = Convert.ToString(oid);
		int pos = oidValue.LastIndexOf(".");
		if (pos == -1)
		{
			protocol.Log("QA" + protocol.QActionID + "|Error when processing buffer write, cannot determine instance from OID, value is " + oidValue, LogType.Error, LogLevel.NoLogging);
			return;
		}

		string instance = oidValue.Substring(pos + 1);
		int portNumber;
		oidValue = oidValue.Substring(0, pos);
		pos = oidValue.LastIndexOf(".");
		if (pos == -1 || !Int32.TryParse(instance, out portNumber))
		{
			protocol.Log("QA" + protocol.QActionID + "|Error when processing buffer write, cannot convert instance from OID or determine bus, value is " + oidValue, LogType.Error, LogLevel.NoLogging);
			return;
		}

		portNumber--;
		oidValue = oidValue.Substring(0, pos + 1);
		Matrix matrix = _matrixStorage.GetMatrix(protocol);
		bool isLocked;
		switch (oidValue)
		{
			case OidValues.OutputConnectedInput:
				int connectedInput;
				if (!TryGetConnectedInput(protocol, matrix.DisplayedInputs, polledValue, portNumber, out connectedInput))
				{
					return;
				}

				if (connectedInput == -1)
				{
					matrix.Outputs[portNumber].DisconnectAll();
				}
				else
				{
					matrix.Outputs[portNumber].Connect(connectedInput);
				}

				break;

			case OidValues.InputLabel:
				matrix.Inputs[portNumber].Label = Convert.ToString(polledValue);
				break;

			case OidValues.OutputLabel:
				matrix.Outputs[portNumber].Label = Convert.ToString(polledValue);
				break;

			case OidValues.InputLock:
				if (TryGetLock(protocol, polledValue, portNumber, true, out isLocked))
				{
					matrix.Inputs[portNumber].IsLocked = isLocked;
				}

				break;

			case OidValues.OutputLock:
				if (TryGetLock(protocol, polledValue, portNumber, false, out isLocked))
				{
					matrix.Outputs[portNumber].IsLocked = isLocked;
				}

				break;

			default:
				protocol.Log("QA" + protocol.QActionID + "|Unknown OID value from write " + oidValue, LogType.Error, LogLevel.NoLogging);
				break;
		}

		matrix.ApplyChanges(protocol);
	}

	internal static class OidValues
	{
		public const string InputLabel = "16.1.3.";
		public const string InputLock = "16.1.4.";
		public const string OutputConnectedInput = "29.1.5.";
		public const string OutputLabel = "29.1.3.";
		public const string OutputLock = "29.1.4.";
	}
}

public sealed class MatrixStorage
{
	private Matrix matrix;

	/// <summary>
	/// Gets the Matrix object. Calls the constructor if needed and makes sure the SLProtocol object is up-to-date.
	/// </summary>
	/// <param name="protocol">Link with Skyline DataMiner.</param>
	/// <returns>Matrix object.</returns>
	public Matrix GetMatrix(SLProtocol protocol)
	{
		if (matrix == null)
		{
			matrix = new Matrix(protocol, 66);  // Involves usage of DiscreetInfo parameter 66, MatrixConnectionsBuffer 4, Matrix 100, RouterControlInputs 1000, RouterControlOutputs 1100
		}
		else
		{
			matrix.SetProtocol(protocol);
		}

		return matrix;
	}
}

public sealed class Matrix : MatrixHelperForMatrixAndTables
{
	private readonly string busNumber;
	private SLProtocol protocol;

	public Matrix(SLProtocol protocol, int discreetInfoParameterId) : base(protocol, discreetInfoParameterId)
	{
		this.protocol = protocol;
		busNumber = Convert.ToString(protocol.GetParameter(1)) + ".";
		if (busNumber == ".")
		{
			throw new InvalidOperationException("The bus number is empty, no polled data is going to match. Please make sure the Device address is filled in, edit the element SNMP settings if needed.");
		}
	}

	public string BusNumber
	{
		get
		{
			return busNumber;
		}
	}

	/// <summary>
	/// Setting the SLProtocol object. This object will be needed in the SetFromUI methods to be able to send the set to the device and needs to be the same object as in the QAction entry point.
	/// </summary>
	/// <param name="protocol">Link with Skyline DataMiner.</param>
	public void SetProtocol(SLProtocol protocol)
	{
		this.protocol = protocol;
	}

	/// <summary>
	/// Gets triggered when crosspoint connections are changed.
	/// </summary>
	/// <param name="set">Information about the updated cross-points.</param>
	protected override void OnCrossPointsSetFromUI(MatrixCrossPointsSetFromUIMessage set)
	{
		HashSet<int> disconnectedOutputs = new HashSet<int>();
		HashSet<int> connectedOutputs = new HashSet<int>();
		foreach (var crossPointSet in set.CrossPointSets)
		{
			if (crossPointSet.State == MatrixCrossPointConnectionState.Connected)
			{
				disconnectedOutputs.Remove(crossPointSet.Output);
				connectedOutputs.Add(crossPointSet.Output);
				AddToBuffer(protocol, QAction.OidValues.OutputConnectedInput + busNumber + Convert.ToString(crossPointSet.Output + 1), Convert.ToString(crossPointSet.Input + 1), true);
			}
			else
			{
				if (!connectedOutputs.Contains(crossPointSet.Output))
				{
					disconnectedOutputs.Add(crossPointSet.Output);
				}
			}
		}

		foreach (int disconnectedOutput in disconnectedOutputs)
		{
			AddToBuffer(protocol, QAction.OidValues.OutputConnectedInput + busNumber + Convert.ToString(disconnectedOutput + 1), "0", true);
		}
	}

	/// <summary>
	/// Gets triggered when the label of an input or output is changed.
	/// </summary>
	/// <param name="set">Information about the changed label.</param>
	protected override void OnLabelSetFromUI(MatrixLabelSetFromUIMessage set)
	{
		string oid = set.Type == MatrixIOType.Input ? QAction.OidValues.InputLabel : QAction.OidValues.OutputLabel;
		AddToBuffer(protocol, oid + busNumber + Convert.ToString(set.Index + 1), set.Label, false);
	}

	/// <summary>
	/// Gets triggered when an input or output is locked or unlocked.
	/// </summary>
	/// <param name="set">Information about the changed lock.</param>
	protected override void OnLockSetFromUI(MatrixLockSetFromUIMessage set)
	{
		string oid = set.Type == MatrixIOType.Input ? QAction.OidValues.InputLock : QAction.OidValues.OutputLock;
		AddToBuffer(protocol, oid + busNumber + Convert.ToString(set.Index + 1), Convert.ToString(set.IsLocked ? (int)ParameterDiscreetValues.LockedValues.Locked : (int)ParameterDiscreetValues.LockedValues.Unlocked), true);
	}

	/// <summary>
	/// Gets triggered when an input or output is enabled or disabled.
	/// </summary>
	/// <param name="set">Information about the changed state.</param>
	protected override void OnStateSetFromUI(MatrixIOStateSetFromUIMessage set)
	{
		if (set.Type == MatrixIOType.Input)
		{
			Inputs[set.Index].IsEnabled = set.IsEnabled;
		}
		else if (set.Type == MatrixIOType.Output)
		{
			Outputs[set.Index].IsEnabled = set.IsEnabled;
		}
		else
		{
			// Do nothing
		}

		ApplyChanges(protocol);
	}

	private static void AddToBuffer(SLProtocol protocol, string oid, string setValue, bool isInteger)
	{
		string currentBuffer = Convert.ToString(protocol.GetParameter(Parameter.routersyslevelwritebuffer_1400));
		string setBuffer = oid + "," + (isInteger ? "1" : "0") + "," + setValue;
		if (String.IsNullOrEmpty(currentBuffer))
		{
			Dictionary<int, object> setParameter = new Dictionary<int, object>();
			setParameter[Parameter.routersyslevelwritebuffer_1400] = setBuffer;
			setParameter[Parameter.routersyslevelwriteoid_1401] = oid;
			if (isInteger)
			{
				setParameter[Parameter.Write.routersyslevelwritevalueinteger_1405] = setValue;
			}
			else
			{
				setParameter[Parameter.Write.routersyslevelwritevalue_1402] = setValue;
			}

			protocol.SetParameters(setParameter.Keys.ToArray(), setParameter.Values.ToArray());
		}
		else
		{
			protocol.SetParameter(Parameter.routersyslevelwritebuffer_1400, currentBuffer + ";" + setBuffer);
		}
	}
}