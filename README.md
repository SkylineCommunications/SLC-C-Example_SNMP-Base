# Example SNMP Base Connector

## About

This package contains an example of an SNMP base connector. This example can help you develop your own SNMP connectors.

## Usage

The input data is sourced from a simulation located in the **Documentation** folder of the package.

To execute the simulation, use the simulator tool you find in the `C:\Skyline DataMiner\Tools\QADeviceSimulator` folder. If this tool is not installed, you can download it from [DataMiner Dojo](https://community.dataminer.services/download/skyline-device-simulator/).

To run the simulation, go to the root of your solution, open a command prompt or PowerShell, and execute the following commands. If DataMiner is installed, the command can be run directly. Otherwise, modify the start command to point to the executable in the downloaded tool:

```cmd
copy /Y "Simulation\connection_0.xml" "C:\QASNMPSimulations\SLC-C-Example_SNMP-Base_1.0.0.X.xml"
start /min "" "C:\Skyline DataMiner\Tools\QADeviceSimulator\QADeviceSimulator.exe" "SLC-C-Example_SNMP-Base_1.0.0.X.xml"
```

## Technical Reference

For more detailed instructions on running the simulation or if you encounter any issues, see the [Skyline Device Simulator documentation](https://docs.dataminer.services/user-guide/Reference/DataMiner_Tools/QADeviceSimulator/TOOQASNMPSimulator.html).
