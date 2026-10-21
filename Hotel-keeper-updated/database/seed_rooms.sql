-- sample rooms
USE housekeeping;
INSERT INTO Rooms (RoomNumber, RoomType, Floor, Description, OccupancyStatus, HousekeepingStatus, ServiceStatus, LastUpdatedAt, Version)
SELECT * FROM (VALUES
 ('101','Standard',1,NULL,'Vacant','Clean','InService',UTC_TIMESTAMP(6),0),
 ('102','Standard',1,NULL,'Occupied','Clean','InService',UTC_TIMESTAMP(6),0),
 ('103','Deluxe',1,NULL,'Vacant','Dirty','InService',UTC_TIMESTAMP(6),0),
 ('104','Suite',1,NULL,'Vacant','Clean','InService',UTC_TIMESTAMP(6),0),
 ('105','Standard',1,NULL,'Occupied','Clean','InService',UTC_TIMESTAMP(6),0),
 ('106','Standard',1,NULL,'Vacant','Cleaning','InService',UTC_TIMESTAMP(6),0),
 ('107','Standard',1,NULL,'Vacant','Clean','InService',UTC_TIMESTAMP(6),0),
 ('108','Deluxe',1,NULL,'Occupied','Clean','InService',UTC_TIMESTAMP(6),0),
 ('109','Suite',1,NULL,'Vacant','Dirty','InService',UTC_TIMESTAMP(6),0),
 ('110','Standard',1,NULL,'Vacant','Clean','OutOfService',UTC_TIMESTAMP(6),0),
 ('201','Standard',2,NULL,'Vacant','Clean','InService',UTC_TIMESTAMP(6),0),
 ('202','Standard',2,NULL,'Occupied','Clean','InService',UTC_TIMESTAMP(6),0),
 ('203','Deluxe',2,NULL,'Vacant','Dirty','InService',UTC_TIMESTAMP(6),0),
 ('204','Suite',2,NULL,'Vacant','Clean','InService',UTC_TIMESTAMP(6),0),
 ('205','Standard',2,NULL,'Occupied','Clean','InService',UTC_TIMESTAMP(6),0),
 ('206','Standard',2,NULL,'Vacant','Cleaning','InService',UTC_TIMESTAMP(6),0),
 ('207','Standard',2,NULL,'Vacant','Clean','InService',UTC_TIMESTAMP(6),0),
 ('208','Deluxe',2,NULL,'Occupied','Clean','InService',UTC_TIMESTAMP(6),0),
 ('209','Suite',2,NULL,'Vacant','Dirty','InService',UTC_TIMESTAMP(6),0),
 ('210','Standard',2,NULL,'Vacant','Clean','InService',UTC_TIMESTAMP(6),0)
) AS v WHERE NOT EXISTS (SELECT 1 FROM Rooms);
