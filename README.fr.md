<div align="center">

<p>
  <a href="README.md"><img height="28" src="https://img.shields.io/badge/English-555?style=for-the-badge" alt="English"></a>
  <a href="README.ar.md"><img height="28" src="https://img.shields.io/badge/%D8%A7%D9%84%D8%B9%D8%B1%D8%A8%D9%8A%D8%A9-555?style=for-the-badge" alt="العربية"></a>
  <a href="README.fr.md"><img height="28" src="https://img.shields.io/badge/Fran%C3%A7ais-8957E5?style=for-the-badge" alt="Français"></a>
</p>

## ⚡ Projet généré avec l'outil [opencode](https://github.com/anomalyco/opencode) CLI

**Modèles gratuits : Big Pickle et DeepSeek V4**

# 🚀 Super Copy

**Copieur de fichiers léger et multi-thread pour Windows XP → 11**<br>
avec une intégration transparente au menu contextuel de l'Explorateur

[![Version 2.0](https://img.shields.io/badge/version-2.0-2EA44F?style=flat-square)](HISTORY.md)
[![Licence : MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![Plateforme : Windows](https://img.shields.io/badge/Platform-Windows%20XP%E2%80%9311-0078D4?style=flat-square&logo=windows&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![.NET Framework 2.0](https://img.shields.io/badge/.NET-Framework%202.0-512BD4?style=flat-square&logo=.net&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![Portable](https://img.shields.io/badge/Type-Portable-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![Étoiles](https://img.shields.io/github/stars/youcef07dz/super-copy?style=flat-square&logo=github&color=yellow)](https://github.com/youcef07dz/super-copy/stargazers)
[![Téléchargements](https://img.shields.io/github/downloads/youcef07dz/super-copy/total?style=flat-square&logo=github&color=orange)](https://github.com/youcef07dz/super-copy/releases)

[**⬇ Télécharger SuperCopy.exe**](https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe) ·
[📖 Fonctionnalités](#-fonctionnalités) ·
[💬 Signaler un problème](https://github.com/youcef07dz/super-copy/issues) ·
[⭐ Mettre une étoile](https://github.com/youcef07dz/super-copy/stargazers)

</div>

> [!TIP]
> **Nouveau dans la v2 :** chaque copie et chaque déplacement est vérifié avec **XXH3-64**. En cas d'écart, la copie défectueuse est supprimée, le fichier écrasé est restauré et la source est conservée.

---

<details>
<summary><b>📑 Table des matières</b></summary>

- [✨ Fonctionnalités](#-fonctionnalités)
- [⚡ Démarrage rapide](#-démarrage-rapide)
- [📥 Téléchargement](#-téléchargement)
- [💻 Ligne de commande](#-ligne-de-commande)
- [🛠 Compilation](#-compilation)
- [📷 Captures d'écran](#-captures-décran)
- [📝 Notes](#-notes)
- [🗂 Historique](#-historique)
- [🤝 Contribuer](#-contribuer)
- [📄 Licence](#-licence)

</details>

---

## ✨ Fonctionnalités

- ⚡ **Rapide** — copie multi-thread avec concurrence intelligente, ajustée selon la taille des fichiers.
- 🖱 **Intégration à l'Explorateur** — clic droit Copier, Couper/déplacer, Copier vers… plus **Coller ici** sur le fond d'un dossier.
- 🔧 **Deux moteurs** — copieur intégré ou Windows Robocopy en option.
- ⚖ **Gestion des conflits** — remplacer, ignorer, garder les deux (renommer), remplacer si plus récent, ou demander d'abord.
- 📊 **Ordre intelligent** — fichiers volumineux d'abord pour un meilleur débit.
- 🛡 **Sécurité vérifiée** — contrôle XXH3-64 à chaque copie/déplacement, vérification de l'espace libre, sauvegarde/restauration en cas d'écrasement.
- 🎯 **Confort** — glisser-déposer, mode compact, rapport d'erreurs avec copie dans le presse-papiers.
- 🌍 **Langues** — anglais, arabe, français ou Auto (suit Windows) ; RTL complet pour l'arabe.
- 📦 **Portable** — un seul fichier, sans installateur ni droits admin ; réglages et presse-papiers écrits de façon atomique.

---

## ⚡ Démarrage rapide

1. **Téléchargez** [`SuperCopy.exe`](https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe) — un fichier portable, sans installateur.
2. **Lancez-le une fois** — la fenêtre Réglages s'ouvre.
3. **Installez le menu** — cliquez sur **Installer le menu** (ou lancez `SuperCopy.exe /install`).
4. **Clic droit** sur un fichier ou un dossier → **Super Copy** → Copier / Couper / Copier vers…
5. **Collez** — clic droit dans un espace vide d'un dossier → **Coller ici**.

> [!TIP]
> Aucun droit administrateur requis — le menu est installé pour l'utilisateur courant dans `HKEY_CURRENT_USER`.

---

## 📥 Téléchargement

<div align="center">

### 🎯 Téléchargement direct

**SuperCopy.exe** — un fichier portable

<a href="https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe">
  <img src="https://img.shields.io/badge/⬇_Download-SuperCopy.exe-2EA44F?style=for-the-badge&logo=windows&logoColor=white" alt="Télécharger SuperCopy.exe">
</a>

<sub>Sans installateur · Sans droits admin · Il suffit de le lancer</sub>

</div>

<details>
<summary><b>Somme SHA-256</b></summary>

```text
CED3BDCF13E3B51A929978838BDC0CE082A570A180AF49F91355E3ADCFB84647
```

</details>

---

## 💻 Ligne de commande

> [!NOTE]
> `SuperCopy.exe` accepte les arguments suivants :

- **`SuperCopy.exe`** — ouvrir les Réglages.
- **`SuperCopy.exe /install`** — ajouter les entrées du menu contextuel.
- **`SuperCopy.exe /uninstall`** — supprimer les entrées du menu contextuel.
- **`SuperCopy.exe /copy <chemins...>`** — mémoriser des éléments à copier.
- **`SuperCopy.exe /cut <chemins...>`** — mémoriser des éléments à déplacer.
- **`SuperCopy.exe /paste <dossier>`** — copier/déplacer les éléments mémorisés ici.
- **`SuperCopy.exe /todevice`** — copier les éléments sélectionnés vers le dossier choisi.
- **`SuperCopy.exe /selftest`** — lancer l'auto-vérification (code 0 si OK).

### Exemples

```bash
# Installer les entrées du menu contextuel
SuperCopy.exe /install

# Mémoriser des fichiers/dossiers pour la copie
SuperCopy.exe /copy "C:\Folder\file1.txt" "D:\Photos\image.jpg"

# Coller les éléments mémorisés dans une destination
SuperCopy.exe /paste "E:\Backup\2026"

# Vérifier l'état de l'installation
SuperCopy.exe /selftest
```

---

## 🛠 Compilation

Lancez `build.cmd` depuis la racine du dépôt.

> [!NOTE]
> Utilise le **`csc.exe` de .NET Framework 3.5** (dossier `v3.5`) avec un **`app.manifest`** explicite `asInvoker` (aucune demande d'élévation). Intègre `SuperCopy.ico` et produit `SuperCopy.exe` à la racine du dépôt.

### Prérequis de compilation

- Windows XP ou ultérieur
- .NET Framework 3.5 installé (fournit `csc.exe` pour compiler)
- Cible d'exécution : .NET Framework 2.0+ (préinstallé sur Windows depuis Vista)
- Aucun paquet NuGet externe requis

---

## 📷 Captures d'écran

### Fenêtre principale

![Fenêtre principale](screenshot/sc2.png)

### Mode compact

![Mode compact](screenshot/sc3.png)

### Réglages

![Réglages](screenshot/sc1.png)

---

## 📝 Notes

> [!IMPORTANT]
> **Utilisateurs de Windows 11** : les entrées apparaissent sous *Afficher plus d'options* (ou appuyez sur `Maj + F10`).

> [!NOTE]
> **Stockage des réglages** : dans `%APPDATA%\SuperCopy\settings.ini`.

> [!TIP]
> **Installation du menu** : requiert `/install` (ou le bouton **Installer le menu** dans les Réglages). **Aucun droit administrateur requis.**

---

## 🗂 Historique

Nouveautés de la v2 et anciennes versions : [HISTORY.md](HISTORY.md). Le code source et le binaire de la v1 sont archivés dans [`old/v1`](old/v1).

---

## 🤝 Contribuer

Les contributions sont les bienvenues ! Voici comment aider :

1. 🍴 **Forkez** le dépôt.
2. 🌿 **Créez** une branche de fonctionnalité (`git checkout -b feature/ma-fonctionnalite`).
3. 💾 **Validez** vos changements (`git commit -m 'Ajouter une fonctionnalité'`).
4. 📤 **Poussez** vers la branche (`git push origin feature/ma-fonctionnalite`).
5. 🔄 **Ouvrez** une Pull Request.

### Façons de contribuer

- 🐛 [Signaler des bugs](https://github.com/youcef07dz/super-copy/issues/new?labels=bug) — aidez-nous à améliorer la stabilité.
- 💡 [Proposer des fonctionnalités](https://github.com/youcef07dz/super-copy/issues/new?labels=enhancement) — partagez vos idées.
- ⭐ [Mettre une étoile](https://github.com/youcef07dz/super-copy/stargazers) — montrez votre soutien.
- 📢 Partager avec les autres — faites passer le mot.

---

## 📄 Licence

Ce projet est sous licence **MIT** — voir le fichier [LICENSE](LICENSE) pour les détails.

<div align="center">

**MIT License — utilisation personnelle et commerciale gratuite**

Fait avec ❤ par [**@youcef07dz**](https://github.com/youcef07dz)

[⬆ Haut de page](#-super-copy)

</div>
