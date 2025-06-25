﻿using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

public class RunMiniLM : MonoBehaviour
{
    public ModelAsset modelAsset;
    public TextAsset vocabAsset;
    const BackendType backend = BackendType.GPUCompute;

    [SerializeField] string string1 = "That is a happy person"; // similarity = 1

    //Choose a string to compare with string1:
    [SerializeField] string string2 = "That is a happy dog"; // similarity = 0.695
    //string string2 = "That is a very happy person"; // similarity = 0.943
    //string string2 = "Today is a sunny day"; // similarity = 0.257

    //Special tokens
    const int START_TOKEN = 101;
    const int END_TOKEN = 102;

    //Store the vocabulary
    string[] tokens;

    const int FEATURES = 384; //size of feature space

    Worker engine, dotScore;

    void Start()
    {
        tokens = vocabAsset.text.Split("\r\n");

        engine = CreateMLModel();

        dotScore = CreateDotScoreModel();

        var tokens1 = GetTokens(string1);
        var tokens2 = GetTokens(string2);

        using Tensor<float> embedding1 = GetEmbedding(tokens1);
        using Tensor<float> embedding2 = GetEmbedding(tokens2);

        float score = GetDotScore(embedding1, embedding2);

        Debug.Log("Similarity Score: " + score);
    }

    float GetDotScore(Tensor<float> A, Tensor<float> B)
    {
        dotScore.Schedule(A, B);
        var output = (dotScore.PeekOutput() as Tensor<float>).DownloadToNativeArray();
        return output[0];
    }

    Tensor<float> GetEmbedding(List<int> tokenList)
    {
        int N = tokenList.Count;
        using var input_ids = new Tensor<int>(new TensorShape(1, N), tokenList.ToArray());
        using var token_type_ids = new Tensor<int>(new TensorShape(1, N), new int[N]);
        int[] mask = new int[N];
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = 1;
        }
        using var attention_mask = new Tensor<int>(new TensorShape(1, N), mask);

        engine.Schedule(input_ids, attention_mask, token_type_ids);

        var output = engine.PeekOutput().ReadbackAndClone() as Tensor<float>;
        return output;
    }

    Worker CreateMLModel()
    {
        var model = ModelLoader.Load(modelAsset);
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var tokenEmbeddings = Functional.Forward(model, inputs)[0];
        var attention_mask = inputs[1];
        var output = MeanPooling(tokenEmbeddings, attention_mask);
        var modelWithMeanPooling = graph.Compile(output);

        return new Worker(modelWithMeanPooling, backend);
    }

    //Get average of token embeddings taking into account the attention mask
    FunctionalTensor MeanPooling(FunctionalTensor tokenEmbeddings, FunctionalTensor attentionMask)
    {
        var mask = attentionMask.Unsqueeze(-1).BroadcastTo(new[] { FEATURES }); //shape=(1,N,FEATURES)
        var A = Functional.ReduceSum(tokenEmbeddings * mask, 1); //shape=(1,FEATURES)
        var B = A / (Functional.ReduceSum(mask, 1) + 1e-9f); //shape=(1,FEATURES)
        var C = Functional.Sqrt(Functional.ReduceSum(Functional.Square(B), 1, true)); //shape=(1,FEATURES)
        return B / C; //shape=(1,FEATURES)
    }

    Worker CreateDotScoreModel()
    {
        var graph = new FunctionalGraph();
        var input1 = graph.AddInput<float>(new TensorShape(1, FEATURES));
        var input2 = graph.AddInput<float>(new TensorShape(1, FEATURES));
        var output = Functional.ReduceSum(input1 * input2, 1);
        var dotScoreModel = graph.Compile(output);
        return new Worker(dotScoreModel, backend);
    }

    List<int> GetTokens(string text)
    {
        //split over whitespace
        string[] words = text.ToLower().Split(null);

        var ids = new List<int>
        {
            START_TOKEN
        };

        string s = "";

        foreach (var word in words)
        {
            int start = 0;
            for (int i = word.Length; i >= 0; i--)
            {
                string subword = start == 0 ? word.Substring(start, i) : "##" + word.Substring(start, i - start);
                int index = Array.IndexOf(tokens, subword);
                if (index >= 0)
                {
                    ids.Add(index);
                    s += subword + " ";
                    if (i == word.Length) break;
                    start = i;
                    i = word.Length + 1;
                }
            }
        }

        ids.Add(END_TOKEN);

        Debug.Log("Tokenized sentence = " + s);

        return ids;
    }

    void OnDestroy()
    {
        dotScore?.Dispose();
        engine?.Dispose();
    }
}
