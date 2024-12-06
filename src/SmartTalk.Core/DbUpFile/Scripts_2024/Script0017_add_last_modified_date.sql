alter table user_account 
    add column last_modified_by int null;

alter table user_account
    add column last_modified_date datetime(3) null;

alter table user_account
    add column creator varchar(128) null;