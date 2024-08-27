# TCP Server-Client Interactive Star Pattern
Welcome to the TCP Server-Client Interactive Star Pattern project! This repository contains a simple yet fascinating local TCP server-client application that demonstrates the power of real-time communication and dynamic graphical interaction between multiple clients.

<p align="center" >
  <a href="#Overview">Overview</a> •
  <a href="#Files">Files</a> •
  <a href="#how-to-use">How To Use</a> •
  <a href="#Images">Images</a>   
</p>

## Overview

This project involves a server program and multiple client programs that communicate over TCP. Each client represents a window on your screen, and all clients are connected to a central server. The clients continuously send their location (x, y coordinates) to the server, which then broadcasts this information to all other connected clients.

The real magic happens on the client side. As each window receives the location data of the other windows, it dynamically updates its own graphical display. Each client window draws lines connecting itself to all other windows, forming a star-like pattern that changes interactively as you move the windows around your screen.

Key Features
+ Real-Time Communication: All clients communicate with the server in real-time, ensuring immediate updates to window positions.
+ Dynamic Graphics: The lines connecting the windows are redrawn instantly as you move any of the windows, creating an interactive star pattern.
+ Scalability: The server can handle multiple clients, allowing for complex patterns as more clients connect and interact.

This project is a great starting point for anyone interested in learning about TCP communication, graphical programming, or just looking for a fun and interactive way to explore client-server architecture.

Feel free to dive into the code, experiment with the setup, and even expand upon the concept to create more complex interactions!

## Files

+ TCPWindowVisual.sln - C# Solution File
+ TCPServer - Project for the server
+ TCPWindowVisual - Project for the client

## How To Use

Run the TCPServer.exe first, and by default (without changing the code), a few clients automatically run, displaying a star pattern.

## Images

![](https://github.com/kychloe567/TCPWindowVisual/blob/master/tcpvisual.png)
