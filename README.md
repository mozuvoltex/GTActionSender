# GTActionSender

## Introduction

GTActionSender is a tool for sending commands to Giggle Tech devices, such as the Giggle Puck. Commands are specified in a csv file as a series of actions which translate into OSC messages sent to the Giggle Tech Router. This allows the device or devices to be driven directly, outside of VRChat.

## Action Specification

The csv file for actions uses the following schema: ActionType (char), DeviceIndex (int), Value (float), Delay (int).

### Action Type

This field is one character that specifies the type of action to be performed. There are currently two actions that are supported:

- `s`: sends a max speed command to the router, using the parameter specified by `max_speed_parameter` in your config.ini file. This applies to all devices, so the following Device Index field is ignored for this action.
- `p`: sends a proximity command to the router, using the parameter specified by `proximity_parameters_multi` in your config.ini file. This action is per-device, unlike the previous action.

### Device Index

This is interpreted as an index into the list of devices specified by `proximity_parameters_multi`, starting at index 0. The command will be sent to that device.

### Value

A value for the action. This is the max speed for `s` actions, and the proximity for `p` actions. This value will be clamped to a value between 0 and 1 inclusive when sending the command.

### Delay

The delay in milliseconds to wait between sending this command and moving on to the next command.

## Using GTActionSender

First, create a csv file describing the command you want to send. There is a sample headpat file you can use as a reference. Note that GTActionSender will automatically send zero proximity commands to all devices prior to exiting, so you don't need to specify them in your csv file. Second, launch the Giggle Tech OSC Router and ensure that it is connected properly using the OSC Sim. Finally, run the tool while providing the path to your config.ini file and csv file like the following:

`GTActionSender.exe --iniPath "C:\path\to\config.ini" --headpatCsv "C:\path\to\headpat.csv"`

Running in a command prompt will print out logging as the script is ran. Whether launched through a command prompt or externally through a tool like SAMMI, the program will exit when it reaches the end of the csv file. GTActionSender can continue to be called multiple times so long as the Router remains active.
