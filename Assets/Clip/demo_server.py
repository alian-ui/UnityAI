from fastapi import FastAPI
from pydantic import BaseModel
from typing import List
import json, os, random

app = FastAPI()

# タグ定義ファイルを読み込み
tags_file = os.path.join(os.path.dirname(__file__), "tags.json")
with open(tags_file, "r", encoding="utf-8") as f:
    tags_data = json.load(f)
TAGS = [tag["en"] for tag in tags_data["tags"]]
TAG_MAPPING = {tag["en"]: tag["ja"] for tag in tags_data["tags"]}

class Req(BaseModel):
    image_b64: str
    top_k: int = 3

@app.post("/classify")
def classify(req: Req):
    # デモ用：ランダムなタグを返す
    import random
    selected_tags = random.sample(TAGS, req.top_k)
    out = []
    for i, tag in enumerate(selected_tags):
        score = random.uniform(0.3, 0.9)
        out.append({
            "tag": tag, 
            "tag_ja": TAG_MAPPING[tag], 
            "score": score
        })
    
    # スコア順にソート
    out.sort(key=lambda x: x["score"], reverse=True)
    return {"top_k": out}

@app.get("/")
def root():
    return {"message": "CLIP Demo Server is running"}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8001)
