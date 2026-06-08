create table if not exists knowledge_scene_company
(
    `id` int primary key auto_increment,
    `scene_id` int not null,
    `company_id` int not null,
    `is_applied` tinyint(1) not null default 0,
    `authorized_at` datetime(3) not null,
    `applied_at` datetime(3) null
) charset=utf8mb4;