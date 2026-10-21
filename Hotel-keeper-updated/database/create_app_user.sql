-- run as admin
CREATE USER IF NOT EXISTS 'housekeeping_app'@'%' IDENTIFIED BY 'change-me';
GRANT SELECT, INSERT, UPDATE, DELETE ON housekeeping.* TO 'housekeeping_app'@'%';
-- migration grants
-- GRANT CREATE, ALTER, INDEX, REFERENCES, DROP ON housekeeping.* TO 'housekeeping_app'@'%';
FLUSH PRIVILEGES;
