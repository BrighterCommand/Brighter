--Table:Messages 
	   
CREATE TABLE Messages          
(
Id BIGSERIAL  PRIMARY KEY,
MessageId UUID UNIQUE NOT NULL ,
Topic VARCHAR(255) NULL ,
MessageType VARCHAR(32) NULL ,
Timestamp TIMESTAMP NULL ,
HeaderBag TEXT NULL ,
Body TEXT NULL
);