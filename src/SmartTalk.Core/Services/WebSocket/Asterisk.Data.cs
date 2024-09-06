using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private Dictionary<string, List<string>> GenerateFoodMenu()
    {
        return new Dictionary<string, List<string>>
        {
            { "小吃", new List<string> { "炸鸡翼", "港式咖喱鱼蛋", "椒盐鸡翼" ,"菠萝油"} },
            { "粥", new List<string> { "皮蛋瘦肉粥", "明火白粥" } },
            { "粉面", new List<string> { "海鮮炒麵", "豉椒牛肉炒河", "鮮虾云吞汤面", "特式炒一丁" } },
            { "饭", new List<string> { "扬州炒饭", "椒盐猪扒饭", "叉烧炒饭" } },
            { "饮料", new List<string> { "可乐", "雪碧", "柠檬茶","港式奶茶"}  }
        };
    }
    
    private List<Food> GenerateDefaultFoods()
    {
        return new List<Food>
        {
            new ()
            {
                Id = "8988366527202311",
                Price = 10.5,
                EnglishName = "Fried Wing",
                CineseName = "炸鸡翼"
            },
            new ()
            {
                Id = "8988366527202312",
                Price = 8.95,
                EnglishName = "HK STyle Curry Fish Ball",
                CineseName = "港式咖喱鱼蛋"
            },
            new ()
            {
                Id = "8988366527202310",
                Price = 10.5,
                EnglishName = "Fried Chicken Wings w. Pepper Salt",
                CineseName = "椒盐鸡翼"
            },
            new ()
            {
                Id = "8988366527267907",
                Price = 9.5,
                EnglishName = "Pres Duck Egg & Chicken Strip Porridge",
                CineseName = "皮蛋瘦肉粥"
            },
            new ()
            {
                Id = "8988366527267906",
                Price = 6.95,
                EnglishName = "Porridge",
                CineseName = "明火白粥"
            },
            new ()
            {
                Id = "8988366527267850",
                Price = 4.95,
                EnglishName = "HK Style Bo-Law Bun",
                CineseName = "菠萝油"
            },
            new ()
            {
                Id = "8988366527267884",
                Price = 13.5,
                EnglishName = "Seafood Chow Mein",
                CineseName = "海鲜炒面"
            },
            new ()
            {
                Id = "8988366527267885",
                Price = 12.95,
                EnglishName = "Beef Chow Fun w. Black Bean & Pepper",
                CineseName = "豉椒牛肉炒河"
            },
            new ()
            {
                Id = "8988366527267872",
                Price = 12.95,
                EnglishName = "Spam & Eggs Fried Instant Noodle",
                CineseName = "特式炒一丁"
            },
            new ()
            {
                Id = "8988366527267902",
                Price = 13.95,
                EnglishName = "Shrimp Wonton Noodle In Soup",
                CineseName = "鲜虾云吞汤面"
            },
            new ()
            {
                Id = "8988366527267866",
                Price = 12.95,
                EnglishName = "Yang-Chow Fried Rice",
                CineseName = "扬州炒饭"
            },
            new ()
            {
                Id = "8988366527267863",
                Price = 13.95,
                EnglishName = "Salt & Pepper Pork Chop",
                CineseName = "椒盐猪扒饭"
            },
            new ()
            {
                Id = "8988366527267874",
                Price = 12.95,
                EnglishName = "BBQ Pork Fried Rice",
                CineseName = "叉烧炒饭"
            },
            new ()
            {
                Id = "8988366527267926",
                Price = 2.95,
                EnglishName = "Coke",
                CineseName = "可乐"
            },
            new ()
            {
                Id = "8988366527267927",
                Price = 2.95,
                EnglishName = "Sprite",
                CineseName = "雪碧"
            },
            new ()
            {
                Id = "8988366527267913",
                Price = 4.5,
                EnglishName = "HK Style Lemon Tea",
                CineseName = "柠檬茶"
            },
            new ()
            {
                Id = "8988366527267910",
                Price = 4.5,
                EnglishName = "HK Style Milk Tea",
                CineseName = "港式奶茶"
            }
        };
    }

    private Dictionary<string, string> GenerateReplyTexts()
    {
        return new Dictionary<string, string>
        {
            { "https://speech-test.sjdistributor.com/data/tts/38a9c16c-0785-4607-add7-5dda93c9c6a5.wav", "你好，有乜可以幫到你？" },
            { "https://speech-test.sjdistributor.com/data/tts/c9de5450-056c-4ad7-a782-a183ea6fcda4.wav", "你好，今日有乜想食？" },
            
            { "https://speech-test.sjdistributor.com/data/tts/e6d34cc8-66f3-4a99-a273-ec18dc5b2c02.wav", "宜家有幾樣小吃賣：菠蘿油、炸鷄翼、椒鹽鷄翼同埋港式咖喱鱼蛋" },
            { "https://speech-test.sjdistributor.com/data/tts/1cd7a460-bec0-46df-8a56-3ed652d66691.wav", "菠蘿油、炸鷄翼、椒鹽鷄翼同埋港式咖喱鱼蛋都有得卖，你需要边样？" },
            { "https://speech-test.sjdistributor.com/data/tts/2dadb08d-3b60-4126-a043-451eb0f3ff1c.wav", "宜家有菠蘿油、炸鷄翼、椒鹽鷄翼同埋港式咖喱鱼蛋卖，你钟意食边样" },
            
            { "https://speech-test.sjdistributor.com/data/tts/f6323baf-22aa-481b-a9d0-9292ed0132e6.wav", "宜家有哩几样粥賣：皮蛋瘦肉粥同埋明火白粥" },
            { "https://speech-test.sjdistributor.com/data/tts/b6e20842-2ff5-4327-aea0-814da7afd009.wav", "皮蛋瘦肉粥同埋明火白粥都有得卖，你需要边样？" },
            { "https://speech-test.sjdistributor.com/data/tts/a0ee9fef-6c0a-4910-b5c2-924e1a0b923b.wav", "宜家有皮蛋瘦肉粥同埋明火白粥卖，你钟意食边样？" },
            
            { "https://speech-test.sjdistributor.com/data/tts/101ff31b-d5d4-4845-8b16-f2c43bcbae47.wav", "宜家有海鮮炒麵、豉椒牛肉炒河、鲜虾云吞湯面同埋特式炒一丁卖，你钟意食边样？" },
            { "https://speech-test.sjdistributor.com/data/tts/f88956d3-b41c-4d8c-b9bd-8357fab82c96.wav", "海鮮炒麵、豉椒牛肉炒河、鲜虾云吞湯面同埋特式炒一丁都有得卖，你需要边样？" },
            { "https://speech-test.sjdistributor.com/data/tts/1ca2a5fe-5ec2-47dc-b36d-88ee76d443f5.wav", "宜家有哩几样粉面賣：海鮮炒麵、豉椒牛肉炒河、鲜虾云吞湯面同埋特式炒一丁" },
            
            { "https://speech-test.sjdistributor.com/data/tts/b1163910-2ee1-430d-b2cf-d85a4221d101.wav", "宜家有扬州炒饭、椒盐猪扒饭同埋叉烧炒饭卖，你钟意食边样？" },
            { "https://speech-test.sjdistributor.com/data/tts/4854f346-a161-4eb2-86cd-784a197dfa14.wav", "扬州炒饭、椒盐猪扒饭同埋叉烧炒饭都有得卖，你需要边样？" },
            { "https://speech-test.sjdistributor.com/data/tts/ba270fc2-2679-44fb-85a1-3318ea68db0d.wav", "宜家有哩几样饭賣：扬州炒饭、椒盐猪扒饭同埋叉烧炒饭" },
            
            { "https://speech-test.sjdistributor.com/data/tts/c014d7e7-9920-4843-b38e-b2dcc949bb6d.wav", "宜家有可乐、雪碧、柠檬茶同埋港式奶茶卖，你中意边样" },
            { "https://speech-test.sjdistributor.com/data/tts/24186ceb-c040-4bbf-89fb-9fc8ef091d0c.wav", "宜家有哩几样飲料賣：可乐、雪碧、柠檬茶同埋港式奶茶卖" },
            
            { "https://speech-test.sjdistributor.com/data/tts/85133179-ba76-4530-aa92-cae27d5154e2.wav", "唔好意思，我哋宜家暂时冇得卖，唔该再睇睇其他？" },
            { "https://speech-test.sjdistributor.com/data/tts/f2e78f1e-9a96-4806-85e4-c156721d8af1.wav", "对唔住，我哋暂时唔提供，或者再睇睇其他？" },
            
            { "https://speech-test.sjdistributor.com/data/tts/644786dd-4dd1-40e3-82d0-0fa731ec59f2.wav", "睇你想食乜，我哋宜家小吃飲料同埋粥粉麵飯樣樣齊全" },
            { "https://speech-test.sjdistributor.com/data/tts/a74ebdd5-4767-44cd-8602-b7f142aa6976.wav", "宜家小吃飲料同埋粥粉麵飯樣樣齊全，你鐘意食邊樣？" },
            { "https://speech-test.sjdistributor.com/data/tts/0c3dfe24-e166-4a27-a804-f39a6c75a4e6.wav", "我哋小吃飲料同埋粥粉麵飯都有得賣，你鐘意邊樣" },
            
            { "https://speech-test.sjdistributor.com/data/tts/207dbe77-77b7-445e-9aa7-ea21f03b01ef.wav", "好，我宜家先帮你记低" },
            { "https://speech-test.sjdistributor.com/data/tts/72f20f2b-92ff-4155-aad8-4d49a7f1a795.wav", "收到，已經記低左啦" },
            { "https://speech-test.sjdistributor.com/data/tts/a76ba77b-c50f-4d2b-ad43-ef3d94da69cc.wav", "好，無問題" }
        };
    }

    private Dictionary<string, string> GenerateFoodAudios()
    {
        return new Dictionary<string, string>
        {
            { "炸鸡翼", "https://speech-test.sjdistributor.com/data/tts/107f1e88-e60f-4844-a794-eaff798dd699.wav" },
            { "港式咖喱魚旦", "https://speech-test.sjdistributor.com/data/tts/4dc182d6-e2c8-4c83-ab4d-7bd5fbfe393f.wav" },
            { "椒盐鸡翼", "https://speech-test.sjdistributor.com/data/tts/90b5f82c-4ee6-425b-b85c-a262fbcdbc96.wav" },
            { "菠萝油", "https://speech-test.sjdistributor.com/data/tts/accc287d-6b61-49fe-bfe8-5e0cd32f440f.wav" },
            { "皮蛋瘦肉粥", "https://speech-test.sjdistributor.com/data/tts/87540ca9-2992-45c8-a5f0-50b9d397ac74.wav" },
            { "明火白粥", "https://speech-test.sjdistributor.com/data/tts/f833af7c-04f8-4790-902b-61f0c32ebbb8.wav" },
            { "海鲜炒面", "https://speech-test.sjdistributor.com/data/tts/c9845f94-8125-4487-9e7f-2dd3500a0930.wav" },
            { "豉椒牛肉炒河", "https://speech-test.sjdistributor.com/data/tts/c921ed97-8e74-43b1-93a2-4aef05f4a26a.wav" },
            { "鲜虾云吞汤面", "https://speech-test.sjdistributor.com/data/tts/c1634549-10bb-468e-8c7b-99cd0243242b.wav" },
            { "特式炒一丁", "https://speech-test.sjdistributor.com/data/tts/2d94dab1-6780-476b-9055-c0138d1f94b6.wav" },
            { "扬州炒饭", "https://speech-test.sjdistributor.com/data/tts/2f706cf0-8ac5-4b7b-ae6d-7af334046272.wav" },
            { "椒盐猪扒饭", "https://speech-test.sjdistributor.com/data/tts/ab3a334c-da3c-4bf5-a501-16ab0cedb775.wav" },
            { "叉烧炒饭", "https://speech-test.sjdistributor.com/data/tts/4d951b50-1ac0-49e1-990d-e1e41e565541.wav" },
            { "可乐", "https://speech-test.sjdistributor.com/data/tts/44558303-864f-45a0-aa81-46f80560a806.wav" },
            { "雪碧", "https://speech-test.sjdistributor.com/data/tts/f8101319-f302-4289-8b72-c4f1132502ed.wav" },
            { "柠檬茶", "https://speech-test.sjdistributor.com/data/tts/a05d5ee8-e8ac-4cea-9718-33731de28909.wav" },
            { "港式奶茶", "https://speech-test.sjdistributor.com/data/tts/b9490741-af33-4aa0-be60-bfc2ac9ec190.wav" },
            { "已经帮你加咗", "https://speech-test.sjdistributor.com/data/tts/e41517cb-5471-47b4-99ae-84fc7f77a645.wav" },
            { "入购物车，其他暂时唔提供", "https://speech-test.sjdistributor.com/data/tts/165a9e8b-dbc2-4afb-8056-6ec1e8b624f5.wav" }
        };
    }

    private Dictionary<string, bool> GenerateGlutenFoods()
    {
        return new Dictionary<string, bool>
        {
            { "炸鸡翼", false },
            { "港式咖喱魚旦", false },
            { "椒盐鸡翼", false },
            { "菠萝油", true },
            { "皮蛋瘦肉粥", false },
            { "明火白粥", false },
            { "海鲜炒面", true },
            { "豉椒牛肉炒河", true },
            { "鲜虾云吞汤面", true },
            { "特式炒一丁", true},
            { "扬州炒饭", false },
            { "椒盐猪扒饭", false },
            { "叉烧炒饭", false },
            { "可乐", false },
            { "雪碧", false },
            { "柠檬茶", false },
            { "港式奶茶", false },
        };
    }
}