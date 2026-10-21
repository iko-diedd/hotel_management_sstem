-- mysql schema

CREATE DATABASE IF NOT EXISTS housekeeping CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE housekeeping;

CREATE TABLE Users (
    Id           INT           NOT NULL AUTO_INCREMENT,
    Name         VARCHAR(100)  NOT NULL,
    Username     VARCHAR(50)   NOT NULL,
    PasswordHash VARCHAR(255)  NOT NULL,
    Role         VARCHAR(20)   NOT NULL,
    IsActive     TINYINT(1)    NOT NULL DEFAULT 1,
    CreatedAt    DATETIME(6)   NOT NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT UX_Users_Username UNIQUE (Username)
) ENGINE=InnoDB;

CREATE TABLE Rooms (
    Id                 INT           NOT NULL AUTO_INCREMENT,
    RoomNumber         VARCHAR(10)   NOT NULL,
    RoomType           VARCHAR(50)   NOT NULL,
    Floor              INT           NOT NULL,
    Description        VARCHAR(500)  NULL,
    OccupancyStatus    VARCHAR(20)   NOT NULL DEFAULT 'Vacant',
    HousekeepingStatus VARCHAR(20)   NOT NULL DEFAULT 'Clean',
    ServiceStatus      VARCHAR(20)   NOT NULL DEFAULT 'InService',-- InService | OutOfService
    LastUpdatedAt      DATETIME(6)   NOT NULL,
    Version            INT           NOT NULL DEFAULT 0,          -- concurrency token
    CONSTRAINT PK_Rooms PRIMARY KEY (Id),
    CONSTRAINT UX_Rooms_RoomNumber UNIQUE (RoomNumber),
    CONSTRAINT CK_Rooms_Floor CHECK (Floor >= 0),
    INDEX IX_Rooms_Status (ServiceStatus, OccupancyStatus, HousekeepingStatus),
    INDEX IX_Rooms_RoomType (RoomType)
) ENGINE=InnoDB;

CREATE TABLE HousekeepingNotices (
    Id             INT           NOT NULL AUTO_INCREMENT,
    RoomId         INT           NOT NULL,
    CreatedById    INT           NOT NULL,
    AssignedToId   INT           NULL,
    Message        VARCHAR(1000) NOT NULL,
    Status         VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    CreatedAt      DATETIME(6)   NOT NULL,
    AcknowledgedAt DATETIME(6)   NULL,
    CompletedAt    DATETIME(6)   NULL,
    Version        INT           NOT NULL DEFAULT 0,
    CONSTRAINT PK_HousekeepingNotices PRIMARY KEY (Id),
    CONSTRAINT FK_HousekeepingNotices_Rooms_RoomId       FOREIGN KEY (RoomId)       REFERENCES Rooms (Id) ON DELETE RESTRICT,
    CONSTRAINT FK_HousekeepingNotices_Users_CreatedById  FOREIGN KEY (CreatedById)  REFERENCES Users (Id) ON DELETE RESTRICT,
    CONSTRAINT FK_HousekeepingNotices_Users_AssignedToId FOREIGN KEY (AssignedToId) REFERENCES Users (Id) ON DELETE RESTRICT,
    INDEX IX_HousekeepingNotices_Status_CreatedAt (Status, CreatedAt),
    INDEX IX_HousekeepingNotices_RoomId (RoomId),
    INDEX IX_HousekeepingNotices_AssignedToId (AssignedToId),
    INDEX IX_HousekeepingNotices_CreatedById (CreatedById)
) ENGINE=InnoDB;

CREATE TABLE RoomStatusHistory (
    Id             BIGINT        NOT NULL AUTO_INCREMENT,
    RoomId         INT           NOT NULL,
    PreviousStatus VARCHAR(20)   NOT NULL,
    NewStatus      VARCHAR(20)   NOT NULL,
    ChangedById    INT           NOT NULL,
    ChangedAt      DATETIME(6)   NOT NULL,
    Reason         VARCHAR(500)  NULL,
    CONSTRAINT PK_RoomStatusHistory PRIMARY KEY (Id),
    CONSTRAINT FK_RoomStatusHistory_Rooms_RoomId      FOREIGN KEY (RoomId)      REFERENCES Rooms (Id) ON DELETE RESTRICT,
    CONSTRAINT FK_RoomStatusHistory_Users_ChangedById FOREIGN KEY (ChangedById) REFERENCES Users (Id) ON DELETE RESTRICT,
    INDEX IX_RoomStatusHistory_RoomId_ChangedAt (RoomId, ChangedAt),
    INDEX IX_RoomStatusHistory_ChangedById (ChangedById)
) ENGINE=InnoDB;

CREATE TABLE AuditLogs (
    Id         BIGINT        NOT NULL AUTO_INCREMENT,
    UserId     INT           NULL,
    Action     VARCHAR(100)  NOT NULL,
    EntityType VARCHAR(50)   NOT NULL,
    EntityId   VARCHAR(50)   NULL,
    Timestamp  DATETIME(6)   NOT NULL,
    Details    VARCHAR(500)  NULL,
    CONSTRAINT PK_AuditLogs PRIMARY KEY (Id),
    CONSTRAINT FK_AuditLogs_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT,
    INDEX IX_AuditLogs_Timestamp (Timestamp),
    INDEX IX_AuditLogs_EntityType_EntityId (EntityType, EntityId),
    INDEX IX_AuditLogs_UserId (UserId)
) ENGINE=InnoDB;
