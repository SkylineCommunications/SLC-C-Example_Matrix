using Skyline.DataMiner.Scripting;

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
			matrix = new Matrix(protocol);  // Involves usage of Dummy parameter ID: 10,  MatrixConnectionsBuffer 4, RouterControlInputs 1000, RouterControlOutputs 1100
		}
		else
		{
			matrix.SetProtocol(protocol);
		}

		return matrix;
	}
}
