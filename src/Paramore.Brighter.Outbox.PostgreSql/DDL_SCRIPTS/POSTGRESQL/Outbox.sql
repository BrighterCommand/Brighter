--Table:Messages 

CREATE TABLE Messages
(
    Id BIGSERIAL PRIMARY KEY,
    MessageId UUID UNIQUE NOT NULL,
    Topic VARCHAR(255) NULL,
    MessageType VARCHAR(32) NULL,
    Timestamp timestamptz NULL,
    CorrelationId uuid NULL,
    ReplyTo VARCHAR(255) NULL,
    ContentType VARCHAR(128) NULL,
    HeaderBag TEXT NULL,
    Body TEXT NULL
);