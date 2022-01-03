using ENBT.NET;


Enbt enbt = new Dictionary<string, Enbt>();

enbt["hello"] = 123;
enbt["aaa"] = 343;
enbt["ssss"] = 463;
enbt["yyyyy"] = 263;
Enbt arr = enbt["yawn"] = new List<Enbt>(4);

arr[1] = 325;
arr[3] = 436;

Console.WriteLine(enbt);