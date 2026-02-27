# Skyline Example - SNMP Base Connector

## About

This package demonstrates how to implement basic and generic SNMP parameters (System parameters and Interface tables) within a connector. It is intended to serve as a base for any SNMP connector, helping you avoid having to reinvent the wheel and ensuring a solid starting point.

## Usage

The input data is sourced from a simulation located in the **Documentation** folder of the package.

To execute the simulation, use the **Skyline Device Simulator** tool that can be found in the `C:\Skyline DataMiner\Tools\QADeviceSimulator` folder. If this tool is not installed, you can download it from [DataMiner Dojo](https://community.dataminer.services/download/skyline-device-simulator/).

To run the simulation, go to the root of your solution, open a command prompt or PowerShell, and execute the following commands. If DataMiner is installed, the command can be run directly. Otherwise, modify the start command to point to the executable in the downloaded tool.

```cmd
copy /Y "Simulation\connection_0.xml" "C:\QASNMPSimulations\SLC-C-Example_SNMP-Base_1.0.0.X.xml"
start /min "" "C:\Skyline DataMiner\Tools\QADeviceSimulator\QADeviceSimulator.exe" "SLC-C-Example_SNMP-Base_1.0.0.X.xml"
```

## Technical Reference

For more detailed instructions on how to run the simulation, or if you encounter any issues, refer to the [Skyline Device Simulator documentation](https://aka.dataminer.services/qa-device-simulator).
