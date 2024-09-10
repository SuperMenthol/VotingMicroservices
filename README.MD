# Voting Microservices
    This project is a part of learning the basics of RabbitMQ and MongoDB. It aims to provide a functionality of voting and/or creating voting procedures to the user, along with managing votes and voting statistics. Key components are:
* RabbitMQ
* MongoDB

Smooth operation is ensured by:
* Voter Web API - allowing to get information on voting procedures or placing a new vote by publishing a message to a classic RabbitMQ queue with a specific routing key
* Database Manager Web API - allowing to create and remove voting procedures. It interacts both with MongoDB (to maintain collections) and RabbitMQ (to create new routing keys and publish messages to a classic, single active customer queue)
* Set of workers that consist of:
    * Votes receiver - which consumes messages from a RabbitMQ queue, validates them and puts them in a MongoDB collection
    * Stats worker - which calculates the results for active procedures, updates them in a MongoDB database and publishes the results on a stream queue in a topic exchange. It operates both on MongoDB database (as a time-based long-running task) and RabbitMQ (as a subscriber to a single active customer queue)
    * Results receiver - which pulls the messages from aforementioned stream queue based on the specified topic
    * Collections cleanup worker - which checks if a procedure votes collection can be safely removed
* Test application that consists of two workers: one for managing voting procedures, and another one for sending votes

It is by no means a production-ready solution, it's a backyard shed project to help me learn the ropes in practice. The features lacking:
* Deployment
* Authentication and authorization