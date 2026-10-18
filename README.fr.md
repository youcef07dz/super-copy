<div align="center">

<p>
  <a href="README.md"><img height="30" src="https://img.shields.io/badge/English-555?style=for-the-badge" alt="English"></a>
  <a href="README.ar.md"><img height="30" src="https://img.shields.io/badge/%D8%A7%D9%84%D8%B9%D8%B1%D8%A8%D9%8A%D8%A9-555?style=for-the-badge" alt="العربية"></a>
  <a href="README.fr.md"><img height="30" src="https://img.shields.io/badge/Fran%C3%A7ais-8957E5?style=for-the-badge" alt="Français"></a>
</p>

# 🚀 Super Copy

**Copieur de fichiers léger et multi-thread pour Windows XP → 11**<br>
avec une intégration transparente au menu contextuel de l'Explorateur

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%20XP%E2%80%9311-0078D4?style=flat-square&logo=windows&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![.NET Framework 2.0](https://img.shields.io/badge/.NET-Framework%202.0-512BD4?style=flat-square&logo=.net&logoColor=white)](https://github.com/youcef07dz/super-copy)
[![Portable](https://img.shields.io/badge/Type-Portable-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![No Installer](https://img.shields.io/badge/Installer-Not%20Required-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![Admin Rights](https://img.shields.io/badge/Admin%20Rights-Not%20Required-2EA44F?style=flat-square)](https://github.com/youcef07dz/super-copy)
[![Stars](https://img.shields.io/github/stars/youcef07dz/super-copy?style=flat-square&logo=github&color=yellow)](https://github.com/youcef07dz/super-copy/stargazers)
[![Downloads](https://img.shields.io/github/downloads/youcef07dz/super-copy/total?style=flat-square&logo=github&color=orange)](https://github.com/youcef07dz/super-copy/releases)

[**⬇ Télécharger SuperCopy.exe**](https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe) ·
[📖 Documentation](#-fonctionnalités) ·
[💬 Signaler un problème](https://github.com/youcef07dz/super-copy/issues) ·
[⭐ Mettre une étoile](https://github.com/youcef07dz/super-copy/stargazers)

</div>

---

## 📑 Table des matières

- [✨ Fonctionnalités](#-fonctionnalités)
- [⚡ Démarrage rapide](#-démarrage-rapide)
- [📥 Téléchargement](#-téléchargement)
- [💻 Ligne de commande](#-ligne-de-commande)
- [🛠 Compilation](#-compilation)
- [📷 Captures d'écran](#-captures-décran)
- [📝 Notes](#-notes)
- [🤝 Contribuer](#-contribuer)
- [📄 Licence](#-licence)

---

## ✨ Fonctionnalités

|  | Fonctionnalité | Description |
|:---:|---|---|
| ⚡ | **Rapide** | Copie multi-thread avec concurrence intelligente, ajustée selon la taille des fichiers |
| 🖱 | **Intégration à l'Explorateur** | Verbes du menu contextuel (Copier, Couper/déplacer, Copier vers…) + *Coller ici* sur le fond de dossier |
| 🔧 | **Moteurs** | Moteur intégré ou Windows Robocopy en option |
| ⚖ | **Conflits** | Remplacer · Ignorer · Garder les deux (renommer) · Remplacer si plus récent · Demander d'abord |
| 📊 | **Ordre intelligent** | Fichiers volumineux d'abord pour un meilleur débit |
| 🛡 | **Sécurité** | Vérification de l'espace libre, vérification MD5 avant suppression de la source |
| 🎯 | **Confort** | Glisser-déposer, mode compact, rapport d'erreurs avec copie dans le presse-papiers |
| 🌍 | **Langues** | Anglais, arabe, français — ou Auto (suit la langue de Windows) ; RTL complet pour l'arabe |
| 📦 | **Portable + sûr** | E/S atomiques pour les réglages/presse-papiers ; menu via HKCU |

---

## ⚡ Démarrage rapide

```
1. Télécharger     →  SuperCopy.exe (fichier portable unique, sans installateur)
2. Lancer une fois →  Ouvre le panneau Réglages
3. Installer menu  →  Cliquez sur « Installer le menu » (ou lancez /install)
4. Clic droit      →  Fichiers/dossiers → Super Copy > Copier / Couper / Copier vers...
5. Coller ici      →  Clic droit dans un espace vide d'un dossier → Coller ici
```

> [!TIP]
> Aucun droit administrateur requis — le menu est installé pour l'utilisateur courant dans `HKEY_CURRENT_USER`.

---

## 📥 Téléchargement

<table>
  <tr>
    <td align="center" width="100%">
      <h3>🎯 Téléchargement direct</h3>
      <p><b>SuperCopy.exe</b> — fichier portable unique</p>
      <a href="https://github.com/youcef07dz/super-copy/raw/refs/heads/main/SuperCopy.exe">
        <img src="https://img.shields.io/badge/⬇_Download-SuperCopy.exe-2EA44F?style=for-the-badge&logo=windows&logoColor=white" alt="Télécharger SuperCopy.exe">
      </a>
      <p><sub>Sans installateur · Sans droits admin · Il suffit de le lancer</sub></p>
    </td>
  </tr>
</table>

**SHA-256 :** `9890A541A4DCC5BBE4DD753135B648F06E77CAEB7D7797946A0396FA1B1F83BB`

---

## 💻 Ligne de commande

> [!NOTE]
> `SuperCopy.exe` accepte les arguments suivants :

| Commande | Action |
|---|---|
| `SuperCopy.exe` | Ouvrir les Réglages |
| `SuperCopy.exe /install` | Ajouter les entrées du menu contextuel |
| `SuperCopy.exe /uninstall` | Supprimer les entrées du menu contextuel |
| `SuperCopy.exe /copy <chemins...>` | Mémoriser des éléments à copier |
| `SuperCopy.exe /cut <chemins...>` | Mémoriser des éléments à déplacer |
| `SuperCopy.exe /paste <dossier>` | Copier/déplacer les éléments mémorisés ici |
| `SuperCopy.exe /todevice` | Copier les éléments sélectionnés vers le dossier choisi |
| `SuperCopy.exe /selftest` | Lancer l'auto-vérification (code 0 si OK) |

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

```bash
build.cmd
```

> [!NOTE]
> Utilise le **`csc.exe` de .NET Framework 3.5** (dossier `v3.5`) avec un **`app.manifest`** explicite `asInvoker` (aucune demande d'élévation). Intègre `SuperCopy.ico` et produit `SuperCopy.exe` à la racine du dépôt.

### Prérequis de compilation

- Windows XP ou ultérieur
- .NET Framework 3.5 installé (fournit `csc.exe` pour compiler)
- Cible d'exécution : .NET Framework 2.0+ (préinstallé sur Windows depuis Vista)
- Aucun paquet NuGet externe requis

---

## 📷 Captures d'écran

<div align="center">

| Fenêtre principale | Mode compact | Réglages |
|:---:|:---:|:---:|
| ![Fenêtre principale](screenshot/sc2.png) | ![Mode compact](screenshot/sc3.png) | ![Réglages](screenshot/sc1.png) |

</div>

---

## 📝 Notes

> [!IMPORTANT]
> **Utilisateurs de Windows 11** : les entrées apparaissent sous *Afficher plus d'options* (ou appuyez sur `Maj + F10`).

> [!NOTE]
> **Stockage des réglages** : dans `%APPDATA%\SuperCopy\settings.ini`.

> [!TIP]
> **Installation du menu** : requiert `/install` (ou le bouton *Installer le menu* dans les Réglages). **Aucun droit administrateur requis.**

---

## 🤝 Contribuer

Les contributions sont les bienvenues ! Voici comment aider :

1. 🍴 **Forkez** le dépôt
2. 🌿 **Créez** une branche de fonctionnalité (`git checkout -b feature/ma-fonctionnalite`)
3. 💾 **Validez** vos changements (`git commit -m 'Ajouter une fonctionnalité'`)
4. 📤 **Poussez** vers la branche (`git push origin feature/ma-fonctionnalite`)
5. 🔄 **Ouvrez** une Pull Request

### Façons de contribuer

- 🐛 [Signaler des bugs](https://github.com/youcef07dz/super-copy/issues/new?labels=bug) — aidez-nous à améliorer la stabilité
- 💡 [Proposer des fonctionnalités](https://github.com/youcef07dz/super-copy/issues/new?labels=enhancement) — partagez vos idées
- ⭐ [Mettre une étoile](https://github.com/youcef07dz/super-copy/stargazers) — montrez votre soutien
- 📢 Partager avec les autres — faites passer le mot

---

## 📄 Licence

Ce projet est sous licence **MIT** — voir le fichier [LICENSE](LICENSE) pour les détails.

<div align="center">

```
MIT License — utilisation personnelle et commerciale gratuite
```

Fait avec ❤ par [**@youcef07dz**](https://github.com/youcef07dz)

[⬆ Haut de page](#-super-copy)

</div>
