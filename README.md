![ChatGPT Image May 21, 2025, 02_46_57 PM](https://github.com/user-attachments/assets/25b59614-fa16-4c90-b05e-26abd891c6a4)
Tasks 1 (Required):
Introduction
In this task we will create a system for continuous processing of scanned images. 
Traditionally such systems consist of multiple independent services. Services could run on the same PC or multiple servers. For instance, the following setup could be applied: 


Data capture service. Usually, data capture services have multiple instances installed on the multiple servers. The main purpose of these services is documents capturing and documents transferring next to the image transformation servers. 


Image transformation services. Also, there can be multiple instances for balancing workload. Such services could perform the following image processing tasks like format converting, text recognition (OCR), sending to other document processing systems. 


Main processing service. The purpose of the service is to monitor and control other services (data capturing and image transformation). 


We will implement simplified model with 2 elements: Data capture service and Processing service. 
Notes! Please discuss with you mentor the following details prior starting the task: 


Which exact message queue to use (e.g., MSMQ/RabbitMQ/Kafka) 


High-level solution architecture  


* use console application as services 
Collecting data processing results 
Implement the main processing service that should do the following: 


Create new queue on startup for receiving results from Data capture services. 


Listen to the queue and store  all incoming messages in a local folder. 


Implement Data capture service which will listen to a specific local folder and retrieve documents of some specific format (i.e., PDF) and send to Main processing service through message queue. 


Try to experiment with your system like sending large files (300-500 Mb), if necessary, configure your system to use another format (i.e. .mp4). Implement mechanism of transfering large files through the message queue in chunks. Processed files shouldn't be corrupted after processing.
For learning purposes assume that there could be multiple Data capture services, but we can have only one Processing server.  
