create table if not exists asterisk_cdr(
    id int auto_increment primary key,
    src varchar(250) not null,
    last_app varchar(250) not null,
    disposition varchar(250) null,
    created_date datetime(3) not null
)