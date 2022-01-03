# Project 4.1 Twitter Clone with Client Simulator - COP5615
- Mandar Palkar
- Siddhi Wadgaonkar

## Problem Statement
To build a Twitter-like Engine clone and test it by simulating multiple clients/users using F# and Akka.NET actors.

## Architecture
The two main components in this system are as below:
1. Twitter-Engine (Server)
This is the centralized Server that acts as the main datastore. However, the system is designed in such a way that all the data required by a service is stored within the same actor, hence ensuring no bottleneck. It supports services like Registering a Client, Registering an User, allowing Users to go Online /Offline, Tweet (simple tweet, tweet with hashtag, tweet with mention, tweet with hashtag and mention), Retweet, Follow/Subscribe to a person or hashtag, Search/Query for tweets with a particular hashtag, Search/Query for tweets that mention a particular User. For any incoming client request, the delegator actor passes on the request to its respective handler based on what kind of request it is.

2. Client Simulator
This Client simulator can simulate a given number of "users". Multiple client processes can be forked to simulate more users. Once the client process starts, it initiates and registers itself with the server. Upon acknowledgement, the client then simulates and registers the given number of users and performs actions like Tweet, Retweet, Follow, Query by hashtag, Query by Mentions, Print feed etc. on regular intervals. The frequency with which a particular user performs actions is based on zipf distribution. The simulator also randomly makes 25% of the users to go offline to simulate live connection. 
// Diagram
Zipf distribution


## What is working?
Write about Project
1. Multiple clients - one server
2. Actions implemented
3. Zipf distribution - explain ranking

## Commands to Run
dotnet run <server_ip>
dotnet run <client_id>

## Maximum Network
Number of Users per Client = 1000
Number of Clients = 10

## Performance Measures
Requests per second = 
Average Time for each request = 
// Graphs?
