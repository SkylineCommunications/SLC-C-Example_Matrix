using System;
using System.Collections.Generic;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;

public static class ParameterDiscreetValues
{
	public enum LockedValues
	{
		Locked = 1,
		Unlocked = 2,
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
				case Parameter.Write.routercontroloutputsvirtualsets_1160:
				case Parameter.Write.routercontroloutputsserializedsets_1161:
					_matrixStorage.GetMatrix(protocol).ProcessParameterSetFromUI(protocol, triggerParameter);
					break;
				case Parameter.Routersysleveloutputs.tablePid:  // table 1200
					ProcessOutputData(protocol);
					break;
				case Parameter.Routersyslevelinputs.tablePid:   // table 1300
					ProcessInputData(protocol);
					break;
				case Parameter.routersysleveltakenextwritebufferitem_1404:
					TakeNextWriteBufferItem(protocol);
					break;
				default:
					return; // Not supported trigger parameter.
			}
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}

	private static bool TryGetConnectedInput(SLProtocol protocol, int displayedInputs, object connectedInput, int output, out int input)
	{
		if (!Int32.TryParse(Convert.ToString(connectedInput), out input))
		{
			protocol.Log($"QA{protocol.QActionID}|Output index '{output + 1}' is connected to an input that can't be converted to a number '{connectedInput}'", LogType.Error, LogLevel.NoLogging);
			return false;
		}

		if (input < 0 && input > displayedInputs)
		{
			protocol.Log($"QA{protocol.QActionID}|Output index '{output + 1}' is connected to an input that is out of range '{connectedInput}' maximum known input is '{displayedInputs}'", LogType.Error, LogLevel.NoLogging);
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
			protocol.Log($"QA{protocol.QActionID}|Provided locked value '{lockedValue}' can't be converted to a number for '{(isInput ? "input " : "output ")}{index + 1}'", LogType.Error, LogLevel.NoLogging);
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
			protocol.Log($"QA{protocol.QActionID}|Unknown output locked value '{lockType}' for '{(isInput ? "input " : "output ")}{index + 1}'", LogType.Error, LogLevel.NoLogging);
			return false;
		}
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
			protocol.Log($"QA{protocol.QActionID}|PK column of the '{type}' table can't be converted to a number. Index '{instanceValue}'", LogType.Error, LogLevel.NoLogging);
			return false;
		}

		if (index <= 0 || index > maxAllowed)
		{
			protocol.Log($"QA{protocol.QActionID}|{type}index '{index}' is out of range, maximum value is '{maxAllowed}'", LogType.Error, LogLevel.NoLogging);
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
			var idx = new uint[]
			{
				Parameter.Routersyslevelinputs.Idx.routersyslevelinputsinstance_1301,
				Parameter.Routersyslevelinputs.Idx.routersyslevelinputsname_1302,
				Parameter.Routersyslevelinputs.Idx.routersyslevelinputslocked_1303,
			};
			object[] tableCols = protocol.GetColumns(Parameter.Routersyslevelinputs.tablePid, idx);
			object[] indexCol = (object[])tableCols[0];
			object[] inputName = (object[])tableCols[1];
			object[] inputLocked = (object[])tableCols[2];

			Matrix matrix = _matrixStorage.GetMatrix(protocol);

			int maximumFoundInputs = 0;
			for (int i = 0; i < indexCol.Length; i++)
			{
				if (!ValidateInstance(protocol, matrix.MaxInputs, indexCol[i], matrix.BusNumber, true, ref maximumFoundInputs, out int index)
					|| !TryGetLock(protocol, inputLocked[i], index, true, out bool isLocked))
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
			protocol.Log($"QA{protocol.QActionID}|Exception when processing input table:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}

	private void ProcessOutputData(SLProtocol protocol)
	{
		try
		{
			var idx = new uint[4]
			{
				Parameter.Routersysleveloutputs.Idx.routersysleveloutputsinstance_1201,
				Parameter.Routersysleveloutputs.Idx.routersysleveloutputsname_1202,
				Parameter.Routersysleveloutputs.Idx.routersysleveloutputslocked_1203,
				Parameter.Routersysleveloutputs.Idx.routersysleveloutputsinputstatus_1204,
			};
			object[] tableCols = protocol.GetColumns(Parameter.Routersysleveloutputs.tablePid, idx);
			object[] indexCol = (object[])tableCols[0];
			object[] outputName = (object[])tableCols[1];
			object[] outputLocked = (object[])tableCols[2];
			object[] connectedInputCol = (object[])tableCols[3];

			Matrix matrix = _matrixStorage.GetMatrix(protocol);

			int maximumFoundOutputs = 0;
			for (int i = 0; i < indexCol.Length; i++)
			{
				if (!ValidateInstance(protocol, matrix.MaxOutputs, indexCol[i], matrix.BusNumber, false, ref maximumFoundOutputs, out int output)
					|| !TryGetLock(protocol, outputLocked[i], output, false, out bool isLocked)
					|| !TryGetConnectedInput(protocol, matrix.DisplayedInputs, connectedInputCol[i], output, out int connectedInput))
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
			protocol.Log($"QA{protocol.QActionID}|Exception when processing output table:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
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
				protocol.Log($"QA{protocol.QActionID}|Error when processing buffer write, cannot find ',', buffer content is {currentBuffer}", LogType.Error, LogLevel.NoLogging);
				protocol.SetParameter(Parameter.routersyslevelwritebuffer_1400, String.Empty);
				return;
			}

			string oidValue = nextValue.Substring(0, pos);
			nextValue = nextValue.Substring(pos + 1);
			pos = nextValue.IndexOf(",");
			if (pos == -1)
			{
				protocol.Log($"QA{protocol.QActionID}|Error when processing buffer write, cannot find second ',', buffer content is {currentBuffer}", LogType.Error, LogLevel.NoLogging);
				protocol.SetParameter(Parameter.routersyslevelwritebuffer_1400, String.Empty);
				return;
			}

			bool isInteger = nextValue.Substring(0, pos) == "1";
			string setValue = nextValue.Substring(pos + 1);
			var paramsToSet = new Dictionary<int, object>
			{
				[Parameter.routersyslevelwritebuffer_1400] = currentBuffer,
				[Parameter.routersyslevelwriteoid_1401] = oidValue,
			};

			if (isInteger)
			{
				paramsToSet[Parameter.Write.routersyslevelwritevalueinteger_1405] = setValue;
			}
			else
			{
				paramsToSet[Parameter.Write.routersyslevelwritevalue_1402] = setValue;
			}

			protocol.SetParameters(paramsToSet);
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|Exception when processing buffer write:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}

	private void ProcessPolledBufferItem(SLProtocol protocol, object oid, object polledValue)
	{
		string oidValue = Convert.ToString(oid);
		int pos = oidValue.LastIndexOf(".");
		if (pos == -1)
		{
			protocol.Log($"QA{protocol.QActionID}|Error when processing buffer write, cannot determine instance from OID, value is '{oidValue}'", LogType.Error, LogLevel.NoLogging);
			return;
		}

		string instance = oidValue.Substring(pos + 1);
		int portNumber;
		oidValue = oidValue.Substring(0, pos);
		pos = oidValue.LastIndexOf(".");
		if (pos == -1 || !Int32.TryParse(instance, out portNumber))
		{
			protocol.Log($"QA{protocol.QActionID}|Error when processing buffer write, cannot convert instance from OID or determine bus, value is '{oidValue}'", LogType.Error, LogLevel.NoLogging);
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
				protocol.Log($"QA{protocol.QActionID}|Unknown OID value from write '{oidValue}'", LogType.Error, LogLevel.NoLogging);
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
