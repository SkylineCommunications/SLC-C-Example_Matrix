using System;
using System.Collections.Generic;

using Skyline.DataMiner.Core.Matrix.Protocol;
using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;

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
				AddToBuffer(protocol, $"{QAction.OidValues.OutputConnectedInput}{busNumber}{crossPointSet.Output + 1}", Convert.ToString(crossPointSet.Input + 1), true);
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
			AddToBuffer(protocol, $"{QAction.OidValues.OutputConnectedInput}{busNumber}{disconnectedOutput + 1}", "0", true);
		}
	}

	/// <summary>
	/// Gets triggered when the label of an input or output is changed.
	/// </summary>
	/// <param name="set">Information about the changed label.</param>
	protected override void OnLabelSetFromUI(MatrixLabelSetFromUIMessage set)
	{
		string oid = set.Type == MatrixIOType.Input ? QAction.OidValues.InputLabel : QAction.OidValues.OutputLabel;
		AddToBuffer(protocol, $"{oid}{busNumber}{set.Index + 1}", set.Label, false);
	}

	/// <summary>
	/// Gets triggered when an input or output is locked or unlocked.
	/// </summary>
	/// <param name="set">Information about the changed lock.</param>
	protected override void OnLockSetFromUI(MatrixLockSetFromUIMessage set)
	{
		string oid = set.Type == MatrixIOType.Input ? QAction.OidValues.InputLock : QAction.OidValues.OutputLock;
		AddToBuffer(protocol, $"{oid}{busNumber}{set.Index + 1}", Convert.ToString(set.IsLocked ? (int)ParameterDiscreetValues.LockedValues.Locked : (int)ParameterDiscreetValues.LockedValues.Unlocked), true);
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
		string setBuffer = $"{oid},{(isInteger ? "1" : "0")},{setValue}";
		if (String.IsNullOrEmpty(currentBuffer))
		{
			var paramsToSet = new Dictionary<int, object>
            {
                [Parameter.routersyslevelwritebuffer_1400] = setBuffer,
                [Parameter.routersyslevelwriteoid_1401] = oid,
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
		else
		{
			protocol.SetParameter(Parameter.routersyslevelwritebuffer_1400, $"{currentBuffer};{setBuffer}");
		}
	}
}