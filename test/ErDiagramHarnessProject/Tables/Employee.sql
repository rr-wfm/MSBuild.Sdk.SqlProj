CREATE TABLE hr.Employee
(
    EmployeeId INT NOT NULL,
    EmployeeName NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_hr_Employee PRIMARY KEY CLUSTERED (EmployeeId)
);
