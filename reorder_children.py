import json
from collections import OrderedDict

path=r"Server\Data\Bodies\humano.json"

errors=[]

def reorder(obj):
    if isinstance(obj, list):
        return [reorder(x) for x in obj]
    if isinstance(obj, dict):
        keys=list(obj.keys())
        if 'children' in keys and keys[-1] != 'children':
            errors.append((keys, obj.get('name', '<unnamed>')))
        items=[]
        for k in keys:
            if k!='children':
                items.append((k,reorder(obj[k])))
        if 'children' in obj:
            items.append(('children', reorder(obj['children'])))
        return OrderedDict(items)
    return obj

with open(path,'r',encoding='utf-8') as f:
    data=json.load(f)
root=reorder(data)
print('Found ordering issues in', errors)
with open(path,'w',encoding='utf-8') as f:
    json.dump(root, f, ensure_ascii=False, indent=2)
print('Rewritten', path)
