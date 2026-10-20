# Approach Document

- In here this document explains how I approached with the given task lists.

## Prepare and running the projects.

1. I went through my github account and first forked the listed repository for my account. Then I cloned it localy and go through the folders and files to figure out the overview level of the file structure. 
2. Once I figured out the strucuture and all then i go through package.json files to note out how the npm run commands mapped to up and run this project. 
3. As I saw earlier stage there were two files with database schema and demo data seeding, I saw in the main pakcage.json contains the database up command to directly dockerize the db with the exisiting docker-compose.yml file. 
4. Start the docker and up the db first and then run the api as listed in the package json. 
5. After I see that the API is running well, i get start to see the project according to the tasks by the start of the frontend web.

## Task 1 

1. I manually tried different filters on the tickets page and it wasn't doing anything. So, I get started with the debugging process from the frontend first to figure out how the current filterings works. 
2. I saw that the frontend useTicketList.ts file is not ready for this. Then look on the API side. There saw that there was commented out it is yet to implement. So I started to fixing this from the backend and then placed necessary endpoint calls on the frontend. 
3. I make sure to run every query from the database side without bothering the API memory.

## Task 2

1. This was to apply business logics to the Ticket submission to automate the time duration calculation, priority level calculation and to determine whome to assign for a ticket. 
2. I went though the chart to determine what the task was expecting at the end and then look for the current system to determine the current status of mentioned features.
3. Applied the logics and features from the API side to automate mentioned tasks on the system.
4. For this to verify that everything works, I went through manual testing procedure myself by creating tickets with different combinations to determine the time is calcualted correct and it is assigning to the correct agent with the matching specialization category.

## Task 3

1. As there was no authentication at the begining, so this is a new feature to the current app. 
2. Added login pages and authentication part from the backend API side. 
3. Password hashing was enabled with Pbkdf2. Jwt token is parsing to the browser with HS256 method. 
4. Manually verify that if the token is removed from browser data is redirecting to the login page again and ask for credentials
5. Wrong passwords emails returns a user readable error message.

## Task 4

1. API dockerization is done. Docker container is directed top listen on port 5080. 
2. Verified the swagger and newly added helth endpoints are working when the docker container started to run. 
3. Frontend also can connect and work well with the docker container. 

## Challenges faced

1. After dockerization done the frontend was not connected on the first time run until I changed the backend path as http://localhost:5080. At that point I go to docker desktop and verified that database and api both are running on the container "northwind-support".
2. In the API build command ran to an error showing vulnerability issue on package "System.Security.Cryptography.Pkcs" under the SupportDesk.Infrastructure project. Then I searched for the issue and there were two suggestions to change the version 9.0.21 or 10.0.13 as the issue was exisitng on earlier builds than the version 9.0.11. I forced to version to run on 10.0.13 and the issue was fixed. 

## AI Usage

* I used AI tools as an coding assist and to make explanations about the tasks also to ask and clear out areas which are not clear for me. Before commiting I went through codes and tested manually to determine is there any glitches exist according to my knowledge and search and went through documentations to clarify things which are out of my knowledge. 