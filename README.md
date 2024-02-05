# Example Matrix Connector

This repository serves as a guide on developing a Matrix Connector.

## Simulation Data

The input data is sourced from a simulation located in the **Simulation** folder at the root of this repository.

To execute the simulation, utilize the simulator tool available in the folder `C:\Skyline DataMiner\Tools\QADeviceSimulator`. If you don't have it installed, you can download the tool from [DataMiner Dojo](https://community.dataminer.services/download/skyline-device-simulator/).

To quickly run the simulation, open a command prompt or PowerShell from the root of your solution and execute the following commands. If DataMiner is installed, the command can be run directly. Otherwise, modify the command to point to the downloaded tool's *.exe*:

```cmd
copy /Y "Simulation\Connection_0.xml" "C:\QASNMPSimulations\SLC-C-Example_Matrix.xml"
start /min "" "C:\Skyline DataMiner\Tools\QADeviceSimulator\QADeviceSimulator.exe" "SLC-C-Example_Matrix.xml"
```

For more detailed instructions on running the simulation or resolving potential issues, refer to the comprehensive documentation available at [docs.dataminer.services](https://docs.dataminer.services/user-guide/Reference/DataMiner_Tools/QADeviceSimulator/TOOQASNMPSimulator.html).
