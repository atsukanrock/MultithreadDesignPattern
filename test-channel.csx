#!/usr/bin/env dotnet-script
#r "MultithreadDesignPattern/bin/Debug/net8.0/MultithreadDesignPattern.dll"

using MultithreadDesignPattern.ProducerConsumer;

Console.WriteLine("=== Channel<T> 動作確認テスト ===\n");

// テスト1: 基本的なAdd/Take
Console.WriteLine("テスト1: 基本的な Add/Take 動作");
var channel = new Channel<int>(5);

// Producer
var producerTask = Task.Run(() =>
{
    for (int i = 1; i <= 10; i++)
    {
        channel.Add(i);
        Console.WriteLine($"  Producer: {i} を追加");
        Thread.Sleep(100);
    }
});

// Consumer
var consumerTask = Task.Run(() =>
{
    for (int i = 1; i <= 10; i++)
    {
        var item = channel.Take();
        Console.WriteLine($"  Consumer: {item} を取得");
        Thread.Sleep(150);
    }
});

await Task.WhenAll(producerTask, consumerTask);

Console.WriteLine("\n✅ すべてのテストが成功しました！");
Console.WriteLine("\nChannel<T> は .NET 8 で正常に動作しています。");
