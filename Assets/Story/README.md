---
license: mit
library_name: unity-sentis
pipeline_tag: text-generation
tags:
  - unity-inference-engine
---

# Tiny Stories in Unity 6 with Inference Engine

This is the [Tiny Stories model](https://huggingface.co/roneneldan/TinyStories-33M) running in Unity 6 with Inference Engine. Tiny Stories is a Large Language Model that was trained on children's stories and can create stories based on the first couple of sentences.

## How to Use

* Create a new scene in Unity 6;
* Install `com.unity.ai.inference` from the package manager;
* Install `com.unity.nuget.newtonsoft-json` from the package manager;
* Add the `RunTinyStories.cs` script to the Main Camera;
* Drag the `tinystories.onnx` asset from the `models` folder into the `Model Asset` field;
* Drag the `vocab.json` asset from the `data` folder into the `Vocab Asset` field;
* Drag the `merges.txt` asset from the `data` folder into the `Merges Asset` field;

## Preview
Enter play mode. If working correctly the predicted text will be logged to the console.

## Inference Engine
Inference Engine is a neural network inference library for Unity. Find out more [here](https://docs.unity3d.com/Packages/com.unity.ai.inference@latest).

## Disclaimer
The model was trained on children's stories so very unlikely to produce undesirable text. As an extra precaution, we removed a few tokens from vocab.json that might not be suitable for younger audiences. The original json can be found on the Tiny Stories original page.