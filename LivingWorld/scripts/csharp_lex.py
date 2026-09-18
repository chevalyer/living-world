"""Checks literals and delimiter balance only; does not type-check or compile C#."""
from pathlib import Path
import re

def string_end(s, i):
    start=i
    m=re.match(r'(?:\$@|@\$|\$|@)?"',s[i:])
    if not m: return None
    prefix=m.group(); i+=len(prefix); verbatim='@' in prefix; interpolated='$' in prefix
    while i<len(s):
        if s[i]=='"':
            if verbatim and s[i:i+2]=='""': i+=2;continue
            return i+1
        if s[i]=='\\' and not verbatim:i+=2;continue
        if interpolated and s[i]=='{':
            if s[i:i+2]=='{{':i+=2;continue
            i=interpolation_end(s,i+1);continue
        if interpolated and s[i:i+2]=='}}':i+=2;continue
        i+=1
    raise ValueError(f'unclosed string at {start}')

def char_end(s,i):
    i+=1
    while i<len(s):
        if s[i]=='\\':i+=2;continue
        if s[i]=="'":return i+1
        i+=1
    raise ValueError('unclosed character literal')

def interpolation_end(s,i):
    level=1
    while i<len(s):
        if s.startswith('//',i):
            n=s.find('\n',i);i=len(s) if n<0 else n;continue
        if s.startswith('/*',i):
            n=s.find('*/',i+2)
            if n<0:raise ValueError('unclosed comment')
            i=n+2;continue
        end=string_end(s,i)
        if end is not None:i=end;continue
        if s[i]=="'":i=char_end(s,i);continue
        if s[i]=='{':level+=1
        if s[i]=='}':
            level-=1
            if level==0:return i+1
        i+=1
    raise ValueError('unclosed interpolation')

def tokens(s):
    i=0;out=[]
    pattern=re.compile(r'(?:0[xX][0-9A-Fa-f]+[uUlL]*|(?:\d+(?:\.\d+)?|\.\d+)(?:[eE][+-]?\d+)?[fFdDmMuUlL]*|[A-Za-z_][A-Za-z_0-9]*|\?\?=|=>|==|!=|<=|>=|\+\+|--|\+=|-=|\*=|/=|&&|\|\||\?\?|\?\.|<<|>>|::|\.\.|.)',re.S)
    while i<len(s):
        if s[i].isspace():i+=1;continue
        start=i
        if s.startswith('//',i):
            n=s.find('\n',i);i=len(s) if n<0 else n;out.append(('comment',s[start:i],start,i));continue
        if s.startswith('/*',i):
            n=s.find('*/',i+2)
            if n<0:raise ValueError('unclosed comment')
            i=n+2;out.append(('comment',s[start:i],start,i));continue
        end=string_end(s,i)
        if end is not None:i=end;out.append(('string',s[start:i],start,i));continue
        if s[i]=="'":i=char_end(s,i);out.append(('char',s[start:i],start,i));continue
        m=pattern.match(s,i)
        if not m:raise ValueError(f'unknown token at {i}')
        i=m.end();out.append(('code',m.group(),start,i))
    return out

def validate(text):
    ts=tokens(text);stack=[];pairs={')':'(',']':'[','}':'{'}
    for kind,value,start,end in ts:
        if kind!='code':continue
        if value in '([{':stack.append((value,start))
        elif value in ')]}':
            if not stack or stack[-1][0]!=pairs[value]:raise ValueError(f'unmatched {value} at line {text[:start].count(chr(10))+1}')
            stack.pop()
    if stack:raise ValueError(f'unclosed delimiter {stack[-1]}')
    return ts

