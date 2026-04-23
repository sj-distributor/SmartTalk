create table if not exists knowledge_scene_folder
(
    `id` int primary key auto_increment,
    `name` varchar(128) not null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null
    ) charset=utf8mb4;

create table if not exists knowledge_scene
(
    `id` int primary key auto_increment,
    `folder_id` int not null,
    `name` varchar(128) not null,
    `description` varchar(2048) null,
    `status` int not null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null
    ) charset=utf8mb4;

create table if not exists knowledge_scene_knowledge
(
    `id` int primary key auto_increment,
    `scene_id` int not null,
    `name` varchar(128) not null,
    `type` int not null,
    `content` text null,
    `file_name` varchar(255) null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null
    ) charset=utf8mb4;

create table if not exists ai_speech_assistant_knowledge_scene_relation
(
    `id` int primary key auto_increment,
    `knowledge_id` int not null,
    `scene_id` int not null,
    `created_at` datetime(3) not null
    ) charset=utf8mb4;

create index idx_knowledge_scene_folder_updated_at on knowledge_scene_folder (`updated_at`);
create index idx_knowledge_scene_folder_created_at on knowledge_scene_folder (`created_at`);

create index idx_knowledge_scene_folder_id on knowledge_scene (`folder_id`);
create index idx_knowledge_scene_updated_at on knowledge_scene (`updated_at`);
create index idx_knowledge_scene_created_at on knowledge_scene (`created_at`);

create index idx_knowledge_scene_knowledge_scene_id on knowledge_scene_knowledge (`scene_id`);
create index idx_knowledge_scene_knowledge_updated_at on knowledge_scene_knowledge (`updated_at`);
create index idx_knowledge_scene_knowledge_created_at on knowledge_scene_knowledge (`created_at`);

create index idx_ai_speech_assistant_knowledge_scene_relation_knowledge_id on ai_speech_assistant_knowledge_scene_relation (`knowledge_id`);
create index idx_ai_speech_assistant_knowledge_scene_relation_scene_id on ai_speech_assistant_knowledge_scene_relation (`scene_id`);
