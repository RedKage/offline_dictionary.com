This project converts [dictionary.com](http://www.dictionary.com)'s SQLite offline dictionary into an [XDXF](https://github.com/soshial/xdxf_makedict) format and [StarDict](http://www.stardict.org/) format.
The code is made so that many other format could be implemented in the future.

The reason I did this is explained in this rant [there](http://tacticalfreak.blogspot.com/2016/03/dictionarycom-as-xdxf.html).

The technical explanations, trivia, etc. can be found [there](http://tacticalfreak.blogspot.com/p/offline-dictionary.html) and [there](http://tacticalfreak.blogspot.com/2016/05/dictionarycom-as-stardict-dictionary.html)

I couldn't include dictionary.com's SQLite offline dictionary as this exceeds GitHub's max file size.
Instead, you can grab it there:
  * [android-08-08-primary.sqlite](https://drive.google.com/file/d/0B4j_jC5UOtTPN2hvdUhINE1JRmM/view?usp=sharing)
  * [android-08-08-primary.sqlite-journal](https://drive.google.com/file/d/0B4j_jC5UOtTPV2FPdVRkMW9abHc/view?usp=sharing)

You need both files. If the journal is missing, you will have SQLException saying something like database is damaged.

---

## License ##
|![http://i.imgur.com/oGGeSQP.png](http://i.imgur.com/oGGeSQP.png)|The license for offline_dictionary.com" is the [WTFPL](http://www.wtfpl.net/): _Do What the Fuck You Want to Public License_.|
|:----------------------------------------------------------------|:--------------------------------------------------------------------------------------------------------------------|
